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
        private const int FindDebounceMs = 150;

        private readonly List<int> _findMatches = new List<int>();
        private int _findCurrentIndex = -1;

        private DispatcherTimer _findDebounceTimer;
        private Action _findDebouncedWork;

        private void Find_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFind();
        }

        private void Find_Click(object sender, RoutedEventArgs e)
        {
            if (FindPopup.IsOpen)
            {
                FindClose_Click(sender, e);
                return;
            }

            OpenFind();
        }

        private void OpenFind()
        {
            ReplacePopup.IsOpen = false;
            FindPopup.IsOpen = true;

            UpdateFindMatches();
            FocusLater(FindTextBox);
        }

        private void FindClose_Click(object sender, RoutedEventArgs e)
        {
            FindPopup.IsOpen = false;

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

        private void DebounceFind(Action work)
        {
            _findDebouncedWork = work;

            if (_findDebounceTimer == null)
            {
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

        private void DropFindMatches()
        {
            if (_findDebounceTimer != null) _findDebounceTimer.Stop();
            _findDebouncedWork = null;

            _findCurrentIndex = -1;

            _findMatches.Clear();
            if (_findMatches.Capacity > 1024) _findMatches.Capacity = 0;

            Editor.ClearMatches();
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
            PublishMatches();
        }

        private void RecomputeMatches()
        {
            _findCurrentIndex = -1;
            TextSearch.FindAll(Editor.Document, FindTextBox.Text, ExactMatchRadio.IsChecked == true, _findMatches);
        }

        private void PublishMatches()
        {
            Editor.SetMatches(_findMatches, FindTextBox.Text.Length, _findCurrentIndex,
                HighlightAllCheck.IsChecked == true);
        }

        private void UpdateFindMatches()
        {
            RecomputeMatches();
            PublishMatches();
            UpdateFindStatus();
        }

        private void GoToMatch(bool forward, bool advance = true)
        {
            FlushPendingFind();

            if (_findMatches.Count == 0)
            {
                PublishMatches();
                UpdateFindStatus();
                return;
            }

            if (forward)
            {
                int searchFrom = advance ? Editor.SelectionStart + Editor.SelectionLength : Editor.SelectionStart;

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
            Editor.ScrollIntoView(start);

            UpdateFindStatus();
            PublishMatches();
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
