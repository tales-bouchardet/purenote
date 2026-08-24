using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PureNote
{
    public partial class MainWindow
    {
        private const string NoFileLabel = "No file";

        private int _lineCount = 1;
        private int _charCount = -1;

        private void UpdateCounts()
        {
            int lines = Editor.LineCount;
            int characters = Editor.TextLength;

            if (lines != _lineCount)
            {
                _lineCount = lines;
                LineCountText.Text = $"{lines} ln";
                LineNumberLayer.SetLineCount(lines);
            }

            if (characters != _charCount)
            {
                _charCount = characters;
                CharCountText.Text = $"{characters} ch";
            }
        }

        private void UpdatePathDisplay()
        {
            BreadcrumbScroll.ToolTip = _currentFilePath;
            BuildBreadcrumbs();
        }

        private void Breadcrumbs_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged) BuildBreadcrumbs();
        }

        private void BuildBreadcrumbs()
        {
            BreadcrumbBar.Children.Clear();

            if (string.IsNullOrEmpty(_currentFilePath))
            {
                BreadcrumbBar.Children.Add(MakeCrumb(NoFileLabel, Theme.Crumb, 0));
                return;
            }

            string[] parts = _currentFilePath.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) BreadcrumbBar.Children.Add(MakeCrumb("›", Theme.Crumb, 6));

                bool leaf = i == parts.Length - 1;

                BreadcrumbBar.Children.Add(MakeCrumb(parts[i],
                    leaf ? (_isDirty ? Theme.Dirty : Theme.CrumbLeaf) : Theme.Crumb, 0));
            }

            TrimBreadcrumbs();
        }

        private void TrimBreadcrumbs()
        {
            double available = BreadcrumbScroll.ActualWidth - CrumbSideMargins;

            if (available <= 0) return;

            BreadcrumbScroll.UpdateLayout();
            if (BreadcrumbBar.ActualWidth <= available) return;

            BreadcrumbBar.Children.Insert(0, MakeCrumb("›", Theme.Crumb, 6));
            BreadcrumbBar.Children.Insert(0, MakeCrumb("…", Theme.Crumb, 0));

            while (BreadcrumbBar.Children.Count >= 5)
            {
                BreadcrumbScroll.UpdateLayout();
                if (BreadcrumbBar.ActualWidth <= available) return;

                BreadcrumbBar.Children.RemoveAt(2);
                BreadcrumbBar.Children.RemoveAt(2);
            }
        }

        private const double CrumbSideMargins = 24;

        private static TextBlock MakeCrumb(string text, Brush brush, double sideMargin)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(sideMargin, 0, sideMargin, 0)
            };
        }
    }
}
