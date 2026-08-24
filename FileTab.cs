using System;
using System.ComponentModel;
using System.Text;

namespace PureNote
{
    internal sealed class FileTab : INotifyPropertyChanged
    {
        private string _name;
        private bool _isDirty;
        private bool _isActive;

        public string FullPath { get; set; }

        public Encoding Encoding { get; set; }
        public string LineEnding { get; set; }

        public TextView.EditorState State { get; set; }

        public string SpillPath { get; set; }

        public string HistoryPath { get; set; }

        public long SourceStamp { get; set; }

        public int CaretOffset { get; set; }
        public int SelectionAnchor { get; set; }
        public double VerticalOffset { get; set; }
        public double HorizontalOffset { get; set; }

        public DateTime LastActive { get; set; }

        public bool IsEvicted
        {
            get { return State == null; }
        }

        public string Name
        {
            get { return _name; }
            set { Set(ref _name, value, "Name"); }
        }

        public bool IsDirty
        {
            get { return _isDirty; }
            set { Set(ref _isDirty, value, "IsDirty"); }
        }

        public bool IsActive
        {
            get { return _isActive; }
            set { Set(ref _isActive, value, "IsActive"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T field, T value, string property)
        {
            if (Equals(field, value)) return;

            field = value;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
