using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace PureNote
{
    public partial class MainWindow
    {
        // Long enough to swallow the gaps inside a typed word, short enough that
        // the results still feel like they arrive with the keystroke. Every
        // recompute walks the whole document — on a 111 MB file a single common
        // letter costs 265 ms and three and a half million matches, and typing
        // "undefined" one letter at a time used to pay that nine times over,
        // once per prefix, with the window stopped for each.
        private const int FindDebounceMs = 150;

        private readonly List<int> _findMatches = new List<int>();
        private int _findCurrentIndex = -1;

        private DispatcherTimer _findDebounceTimer;
        private Action _findDebouncedWork;

        private void Find_Click(object sender, RoutedEventArgs e)
        {
            // Searching a document that is still arriving would report matches
            // against whatever prefix happens to be in yet, and miss the rest.
            if (IsLoading)
            {
                ReportBusyLoading();
                return;
            }

            FindPopup.IsOpen = !FindPopup.IsOpen;

            if (FindPopup.IsOpen)
            {
                ReplacePopup.IsOpen = false;
                UpdateFindMatches();

                // Deferred: a top-level MenuItem click leaves the menu holding
                // keyboard focus as it unwinds, which would pull focus back off.
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    FindTextBox.Focus();
                    FindTextBox.SelectAll();
                }));
            }
            else
            {
                DropFindMatches();
            }
        }

        private void FindClose_Click(object sender, RoutedEventArgs e)
        {
            FindPopup.IsOpen = false;

            // Drops the match list rather than just the rectangles: a closed find
            // has no use for either, and a search queued a moment ago would
            // otherwise still fire and walk the whole document for nothing.
            DropFindMatches();
        }

        private void FindTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            DebounceFind(() =>
            {
                RecomputeMatches();
                GoToMatch(forward: true, advance: false);
            });
        }

        // Coalesces a burst of keystrokes into one search. Only the typing paths
        // go through here: opening the popup, stepping between matches and
        // switching match mode are all single deliberate actions, and making the
        // user wait out a timer for those would be latency for its own sake.
        private void DebounceFind(Action work)
        {
            _findDebouncedWork = work;

            if (_findDebounceTimer == null)
            {
                // Input priority so a long search takes its turn against the
                // typing that queued it rather than cutting in front.
                _findDebounceTimer = new DispatcherTimer(DispatcherPriority.Input)
                {
                    Interval = TimeSpan.FromMilliseconds(FindDebounceMs)
                };

                _findDebounceTimer.Tick += (s, e) =>
                {
                    _findDebounceTimer.Stop();

                    Action pending = _findDebouncedWork;
                    _findDebouncedWork = null;
                    if (pending != null) pending();
                };
            }

            // Restarting is what makes it a debounce rather than a throttle: the
            // search happens once the typing pauses, not every 150 ms through it.
            _findDebounceTimer.Stop();
            _findDebounceTimer.Start();
        }

        private void FlushPendingFind()
        {
            if (_findDebouncedWork == null) return;

            _findDebounceTimer.Stop();

            Action pending = _findDebouncedWork;
            _findDebouncedWork = null;
            pending();
        }

        // Called when the document underneath the matches is replaced. The
        // offsets in hand index the outgoing document, and the incoming one is
        // shorter for as long as it is still streaming in — GetRectFromCharacter
        // Index throws rather than clamps when handed an offset past the end, and
        // the highlight layer redraws on the scroll events the load itself
        // raises. Leaving them in place turns opening a file with Find open into
        // an unhandled ArgumentOutOfRangeException.
        private void DropFindMatches()
        {
            if (_findDebounceTimer != null) _findDebounceTimer.Stop();
            _findDebouncedWork = null;

            _findCurrentIndex = -1;

            // A common term in a large file leaves a list holding millions of
            // ints; Clear on its own keeps every byte of that capacity.
            _findMatches.Clear();
            if (_findMatches.Capacity > 1024) _findMatches.Capacity = 0;

            ClearHighlights();
        }

        private void FindTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                GoToMatch(forward: !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                FindClose_Click(sender, e);
                e.Handled = true;
            }
        }

        private void FindNext_Click(object sender, RoutedEventArgs e)
        {
            GoToMatch(forward: true);
        }

        private void FindPrevious_Click(object sender, RoutedEventArgs e)
        {
            GoToMatch(forward: false);
        }

        private void MatchMode_Changed(object sender, RoutedEventArgs e)
        {
            RecomputeMatches();
            GoToMatch(forward: true, advance: false);
        }

        private void HighlightOption_Changed(object sender, RoutedEventArgs e)
        {
            RefreshHighlights();
        }

        private void RecomputeMatches()
        {
            _findCurrentIndex = -1;
            TextSearch.FindAll(DocumentText, FindTextBox.Text, ExactMatchRadio.IsChecked == true, _findMatches);
        }

        // For callers that don't follow up with GoToMatch — which would otherwise
        // redraw the highlight layer and status a second time for the same edit.
        private void UpdateFindMatches()
        {
            RecomputeMatches();
            RefreshHighlights();
            UpdateFindStatus();
        }

        // advance: true for an explicit Next (skip past the current match), false
        // while the search term is still being typed — there the current match is
        // the one the user is refining, so re-searching must not step over it.
        private void GoToMatch(bool forward, bool advance = true)
        {
            // The offsets are about to be handed to Editor.Select, which throws
            // rather than clamps past the end of the document. A debounced search
            // still in flight means they were computed against the text as it
            // stood before the last edit, so settle it before trusting them —
            // which also makes Enter answer immediately instead of waiting out
            // the timer.
            FlushPendingFind();

            if (_findMatches.Count == 0) return;

            // The list is ascending, so locate the insertion point rather than
            // scanning it — a common term in a large file yields tens of thousands
            // of matches and this runs on every keystroke in the search box.
            if (forward)
            {
                int searchFrom = advance
                    ? Editor.SelectionStart + Editor.SelectionLength
                    : Editor.SelectionStart;

                int idx = _findMatches.BinarySearch(searchFrom);
                if (idx < 0) idx = ~idx;
                _findCurrentIndex = idx < _findMatches.Count ? idx : 0;
            }
            else
            {
                int idx = _findMatches.BinarySearch(Editor.SelectionStart);
                if (idx < 0) idx = ~idx;
                _findCurrentIndex = idx > 0 ? idx - 1 : _findMatches.Count - 1;
            }

            int start = _findMatches[_findCurrentIndex];

            Editor.Select(start, FindTextBox.Text.Length);
            ScrollToOffset(start);

            UpdateFindStatus();
            RefreshHighlights();
        }

        private void UpdateFindStatus()
        {
            if (string.IsNullOrEmpty(FindTextBox.Text))
            {
                FindStatusText.Text = "";
            }
            else if (_findMatches.Count == 0)
            {
                FindStatusText.Text = "No matches";
            }
            else
            {
                int current = _findCurrentIndex >= 0 ? _findCurrentIndex + 1 : 0;
                FindStatusText.Text = $"{current} of {_findMatches.Count} matches";
            }
        }
    }
}
