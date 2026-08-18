using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
                FindTextBox.Focus();
                FindTextBox.SelectAll();
                UpdateFindMatches();
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
            UpdateFindMatches();
            GoToMatch(forward: true);
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
            UpdateFindMatches();
            GoToMatch(forward: true);
        }

        private void HighlightOption_Changed(object sender, RoutedEventArgs e)
        {
            RefreshHighlights();
        }

        private void UpdateFindMatches()
        {
            _findCurrentIndex = -1;
            TextSearch.FindAll(Editor.Text, FindTextBox.Text, ExactMatchRadio.IsChecked == true, _findMatches);

            RefreshHighlights();
            UpdateFindStatus();
        }

        private void GoToMatch(bool forward)
        {
            if (_findMatches.Count == 0) return;

            if (forward)
            {
                int searchFrom = Editor.SelectionStart + Editor.SelectionLength;
                int idx = _findMatches.FindIndex(m => m >= searchFrom);
                _findCurrentIndex = idx >= 0 ? idx : 0;
            }
            else
            {
                int searchFrom = Editor.SelectionStart;
                int idx = _findMatches.FindLastIndex(m => m < searchFrom);
                _findCurrentIndex = idx >= 0 ? idx : _findMatches.Count - 1;
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
