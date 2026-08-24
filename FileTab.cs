using System;
using System.ComponentModel;
using System.Text;

namespace PureNote
{
    // One open document.
    //
    // Everything that used to be a field on MainWindow - the path, the encoding,
    // the line ending, whether there is unsaved work - lives here now, one set
    // per tab, and the window reads whichever tab is in front.
    //
    // The split that matters is between what a tab keeps and what it can be made
    // to let go of. The metadata above is a few dozen bytes and stays; State is
    // the document, its undo history and its line index, which on a large file is
    // most of what the program is holding. A tab that is not in front and has not
    // been touched for a while gives State back - to disk if it has unsaved work
    // in it, to nothing at all if the file on disk already says the same thing.
    //
    // So an unselected tab costs its name and its path, and the file it came from
    // stays on disk where it already was.
    internal sealed class FileTab : INotifyPropertyChanged
    {
        private string _name;
        private bool _isDirty;
        private bool _isActive;

        // Null for a document that has never been saved anywhere.
        public string FullPath { get; set; }

        public Encoding Encoding { get; set; }
        public string LineEnding { get; set; }

        // The document and its history, or null when the tab has been emptied.
        public TextView.EditorState State { get; set; }

        // Where the unsaved work went when the tab was emptied. Null unless the
        // tab was carrying changes at the moment it was let go of.
        public string SpillPath { get; set; }

        // Where the undo history went. Kept apart from the document rather than
        // written into it, so the spill above stays a plain readable file - what
        // a crashed session leaves behind should be something a person can open -
        // and so history that cannot be read back costs nothing but itself.
        public string HistoryPath { get; set; }

        // What the file on disk looked like when a clean tab was emptied: its
        // length and the moment it was last written, folded together.
        //
        // A clean tab is reloaded from that file, and its history is a list of
        // positions in what that file said. If something else has written to it
        // since, the positions are about text that is no longer there and undo
        // would put characters back in the wrong places. Zero means no claim.
        public long SourceStamp { get; set; }

        // Kept whether or not State is, because they are two ints and a pair of
        // doubles and they are what makes coming back to a tab feel like
        // returning rather than reopening.
        public int CaretOffset { get; set; }
        public int SelectionAnchor { get; set; }
        public double VerticalOffset { get; set; }
        public double HorizontalOffset { get; set; }

        // When this tab was last in front. The clock the eviction sweep reads.
        public DateTime LastActive { get; set; }

        // True while the tab holds nothing: either it was never loaded or it has
        // been emptied. Clicking it loads it back, from the spill file if there
        // is one and from the original file if there is not.
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

            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(property));
        }
    }
}
