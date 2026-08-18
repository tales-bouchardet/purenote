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
        private readonly List<int> _findMatches = new List<int>();
        private int _findCurrentIndex = -1;

        private void Find_Click(object sender, RoutedEventArgs e)
        {
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
                HighlightLayer.Children.Clear();
            }
        }

        private void FindClose_Click(object sender, RoutedEventArgs e)
        {
            FindPopup.IsOpen = false;
            HighlightLayer.Children.Clear();
        }

        private void FindTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RecomputeMatches();
            GoToMatch(forward: true, advance: false);
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
            TextSearch.FindAll(Editor.Text, FindTextBox.Text, ExactMatchRadio.IsChecked == true, _findMatches);
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
