using System.ComponentModel;
using DriveCrawl_Win32_App.Models;

namespace DriveCrawl_Win32_App.ViewModels
{
    /// <summary>
    /// One row in the Search Locations dialog: a working copy of a saved location (or a new one) plus its UI state.
    /// </summary>
    public class SearchLocationItemViewModel : INotifyPropertyChanged
    {
        private readonly string _originalPath;
        private readonly int _originalPriority;
        private readonly bool _originalIsExternal;
        private bool _isMarkedForRemoval;
        private string? _overlapWarning;

        public int Id { get; }
        public string Path { get; private set; }
        public int Priority { get; private set; }
        public bool IsExternal { get; private set; }
        public bool IsOnline { get; private set; }

        public bool IsNew => Id == 0;

        public bool IsModified => !IsNew &&
            (!string.Equals(Path, _originalPath, StringComparison.Ordinal) ||
             Priority != _originalPriority ||
             IsExternal != _originalIsExternal);

        public string PriorityLabel => $"P{Priority}";

        public string StatusText
        {
            get
            {
                if (!IsOnline)
                    return IsExternal ? "Offline · drive not connected" : "Offline · folder not found";
                return IsExternal ? "External" : "Internal";
            }
        }

        public bool IsMarkedForRemoval
        {
            get => _isMarkedForRemoval;
            set
            {
                if (_isMarkedForRemoval == value) return;
                _isMarkedForRemoval = value;
                OnPropertyChanged(nameof(IsMarkedForRemoval));
            }
        }

        public string? OverlapWarning
        {
            get => _overlapWarning;
            set
            {
                if (_overlapWarning == value) return;
                _overlapWarning = value;
                OnPropertyChanged(nameof(OverlapWarning));
                OnPropertyChanged(nameof(HasOverlapWarning));
            }
        }

        public bool HasOverlapWarning => !string.IsNullOrEmpty(OverlapWarning);

        public SearchLocationItemViewModel(int id, string path, int priority, bool isExternal, bool isOnline)
        {
            Id = id;
            Path = _originalPath = path;
            Priority = _originalPriority = priority;
            IsExternal = _originalIsExternal = isExternal;
            IsOnline = isOnline;
        }

        public static SearchLocationItemViewModel FromModel(WatchedFolders folder, bool isOnline) =>
            new(folder.Id, folder.Path, folder.Priority, folder.IsExternal != 0, isOnline);

        public WatchedFolders ToModel() => new()
        {
            Id = Id,
            Path = Path,
            Priority = Priority,
            IsExternal = IsExternal ? 1 : 0
        };

        internal void Update(string path, int priority, bool isExternal, bool isOnline)
        {
            Path = path;
            Priority = priority;
            IsExternal = isExternal;
            IsOnline = isOnline;
            // A null name tells WPF that every property changed.
            OnPropertyChanged(null);
        }

        private void OnPropertyChanged(string? propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
