using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;

namespace PureNote
{
    public partial class MainWindow
    {
        // Opening a large file used to mean one silent freeze: the window did not
        // appear at all until the editor had laid out every line, so for twenty
        // seconds purenote looked dead.
        //
        // Measured on a 111 MB, 3.1-million-line file: reading it off disk,
        // decoding it and normalising its line endings come to under half a second
        // between them. The other eighteen seconds are a single WPF layout pass.
        // TextBoxView measures every line in the document the first time it is
        // asked for a size, it does that on the UI thread, and there is no way to
        // move it off or to interrupt it partway.
        //
        // It can, though, be paid a slice at a time. AppendText costs the same per
        // character whether the editor is empty or already holds twenty megabytes
        // — measured flat at ~47 ms per 256 KB either way — so feeding the
        // document in over many dispatcher turns costs the same eighteen seconds
        // in total while handing the window back between every slice. The file is
        // readable and scrollable from the first one, and the rest arrives
        // underneath it.
        //
        // An earlier attempt at this went quadratic, and it is worth being precise
        // about why, because the trap is easy to walk back into: it was not the
        // appending. Every slice ends a dispatcher turn, every turn ran the queued
        // status-bar update, and that update read Editor.Text — which rebuilds the
        // whole document into a fresh string, hundreds of times over. Nothing on
        // this path may ask the editor for the text, or for anything it would have
        // to walk the text to answer. Both counts are taken once, up front, from
        // the bytes rather than from the editor, and IsLoading keeps the ordinary
        // edit path out of the way until the load is done.
        //
        // The slices are cut from the file's bytes, not from a decoded string —
        // see DocumentDecoder for why that halves what opening a file costs in
        // memory.

        // Goes in before the first paint, so it buys a visible window rather than
        // a responsive one; small enough that it costs nothing either way.
        private const int FirstChunkBytes = 64 * 1024;

        // Each slice blocks the UI thread for as long as it takes to lay out, so
        // this is the longest stall the user can be made to sit through — and,
        // inversely, what the load costs over and above one flat pass, since every
        // slice ends a dispatcher turn and each turn carries a repaint.
        //
        // On the 111 MB file: at 20 ms the window answered in 11 ms on average and
        // the load took 26 seconds; at 40 ms it answers in 22 and takes 21.5. Past
        // that the curve flattens against the 18.5 seconds the layout costs no
        // matter how it is sliced, so there is little left to buy and it would be
        // paid for in latency. Forty is the corner: still far inside the tenth of
        // a second at which a delay starts to read as a stall.
        private const double SliceBudgetMs = 40;

        private const int MinChunkBytes = 8 * 1024;
        private const int MaxChunkBytes = 4 * 1024 * 1024;

        // Holds the file's bytes for as long as the load runs, and nothing once it
        // ends — which is what makes releasing it the last thing a load does.
        private DocumentDecoder _loading;
        private int _chunkBytes;
        private int _loadGeneration;

        private bool IsLoading
        {
            get { return _loading != null; }
        }

        private void BeginLoad(DocumentDecoder decoder, DocumentShape shape)
        {
            CancelLoad();

            // The document these were built against is being replaced. Both hold
            // a full copy of it, and the find offsets are about to index a
            // shorter one — see DropFindMatches for why that is a crash and not
            // just a wrong highlight.
            DropFindMatches();
            TextSearch.ForgetDocument();
            _textSnapshot = null;

            // Undo stays off across the load: none of the slices is an edit the
            // user made, and Ctrl+Z must not walk back through them.
            Editor.IsUndoEnabled = false;

            if (decoder.ByteLength <= FirstChunkBytes)
            {
                string all = decoder.NextSlice(decoder.ByteLength);

                Editor.Text = all;
                Editor.CaretIndex = 0;
                Editor.ScrollToHome();
                CompleteLoad(all.Length);
                return;
            }

            _loading = decoder;
            _chunkBytes = FirstChunkBytes;

            Editor.Text = decoder.NextSlice(FirstChunkBytes);
            Editor.CaretIndex = 0;

            // Read-only until the whole file is in. The text already on screen is
            // real, and keeping its offsets valid under the appends would be no
            // trouble — but undo is off for the duration, so an edit made now
            // would be one the user could not take back.
            Editor.IsReadOnly = true;

            // Measured from the bytes before any of them were decoded for real, so
            // the footer tells the truth about the file from the first frame and,
            // more to the point, nothing in the loop below ever has to ask the
            // editor to count anything.
            _rawLength = shape.Length;
            _lineCount = shape.LineCount;
            if (_lineNumbersEnabled) LineNumberLayer.SetLineCount(_lineCount);

            LineCountText.Text = $"{_lineCount} ln";
            CharCountText.Text = $"{CountDisplayCharacters()} ch";

            LoadProgressSeparator.Visibility = Visibility.Visible;
            LoadProgressText.Visibility = Visibility.Visible;
            ShowProgress();

            QueueNextSlice();
        }

        // Input, and the exact priority is the whole difference between a window
        // that can be read while it loads and one that only looks like it can.
        //
        // The dispatcher runs the highest queued priority first and goes round
        // robin only within one. Queue the pump above Input — at Loaded, say —
        // and it wins against every wheel, click and keypress for as long as the
        // load lasts: measured over a 3.5-second load with a probe posted at
        // Input, not one input item ran. The window still repaints, because
        // Render outranks both, so it looks alive and simply ignores the mouse.
        // Queue it below, at Background, and the opposite happens — the pointer
        // moving over the window is enough to hold the file off indefinitely,
        // which is what a probe run there showed: it never finished at all.
        //
        // At Input the pump takes its turn in the same queue as the input it
        // would otherwise starve, so the two interleave one for one. Input waits
        // land at the slice budget — 40 ms median, 48 at the 95th — and the load
        // takes no longer for it.
        private void QueueNextSlice()
        {
            int generation = _loadGeneration;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => LoadSlice(generation)));
        }

        private void LoadSlice(int generation)
        {
            if (generation != _loadGeneration || _loading == null) return;

            Stopwatch timer = Stopwatch.StartNew();

            Editor.AppendText(_loading.NextSlice(_chunkBytes));

            // The append only marks the layout dirty; the measure that is the real
            // cost happens on a later pass. Forcing it here is what puts it inside
            // the slice, where it can be timed and budgeted for, instead of
            // landing unmeasured on whatever turn WPF chooses.
            Editor.UpdateLayout();

            timer.Stop();

            if (_loading.AtEnd)
            {
                // Taken from what the decoder actually wrote rather than from the
                // measured total, so a disagreement between the two could never
                // leave the tracked length describing a document the editor does
                // not hold.
                CompleteLoad(_loading.CharsProduced);
                return;
            }

            _chunkBytes = NextChunkSize(_chunkBytes, timer.Elapsed.TotalMilliseconds);

            ShowProgress();
            QueueNextSlice();
        }

        // Per-byte cost swings by more than an order of magnitude with the shape
        // of the file — the layout is priced per line, so a megabyte of 30-byte
        // lines costs many times what a megabyte of 600-byte ones does — and by
        // machine on top of that. So rather than guess a size, aim at the time
        // budget and correct towards it after every slice.
        private static int NextChunkSize(int current, double elapsedMs)
        {
            // A slice fast enough to land on the clock's resolution says nothing
            // useful about the ratio, only that there is room to grow.
            double scale = elapsedMs < 1 ? 2 : SliceBudgetMs / elapsedMs;

            // Held to doubling or halving per step, so one slow slice — a GC, the
            // user dragging the window — cannot fling the size somewhere it takes
            // several more slices to walk back from.
            if (scale > 2) scale = 2;
            if (scale < 0.5) scale = 0.5;

            double next = current * scale;

            if (next < MinChunkBytes) return MinChunkBytes;
            if (next > MaxChunkBytes) return MaxChunkBytes;
            return (int)next;
        }

        private void ShowProgress()
        {
            long percent = 100L * _loading.BytesRead / _loading.ByteLength;
            LoadProgressText.Text = percent.ToString(CultureInfo.InvariantCulture) + "% loaded";
        }

        private void CancelLoad()
        {
            _loadGeneration++;
            _loading = null;

            EndLoadingState();
        }

        private void CompleteLoad(int length)
        {
            // Releases the file's bytes: from here the editor is the only thing
            // holding the document, and on a large file that is the difference
            // between two copies resident and one.
            _loading = null;

            EndLoadingState();

            // Deliberately leaves the caret and the scroll where they are. The
            // point of streaming is that the file can be read while it loads, and
            // the reader is very likely to be somewhere down the document by the
            // time the last slice lands — sending them back to the top at that
            // moment would undo the reading they just did. Both start at the top
            // anyway; whoever moved them since is the user.
            ResetCounts(length);

            // A find left open across an open-file goes stale twice over — wrong
            // document, and the edit path that would normally refresh it was
            // stood down for the whole load.
            if (FindPopup.IsOpen) UpdateFindMatches();
        }

        private void EndLoadingState()
        {
            Editor.IsReadOnly = false;
            Editor.IsUndoEnabled = true;

            LoadProgressSeparator.Visibility = Visibility.Collapsed;
            LoadProgressText.Visibility = Visibility.Collapsed;
        }

        // Find, Replace, Save and the encoding check all need the whole document,
        // and until the load finishes the editor holds only a prefix of it.
        private void ReportBusyLoading()
        {
            AppMessageBox.Show(this,
                "The file is still loading.\n\nWait for it to finish.",
                "Still loading", MessageBoxButton.OK);
        }
    }
}
