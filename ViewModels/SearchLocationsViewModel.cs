using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Services;
using Microsoft.Data.Sqlite;

namespace DriveCrawl_Win32_App.ViewModels
{
    public class PriorityOptionViewModel : INotifyPropertyChanged
    {
        private readonly Action<int> _onSelected;
        private bool _isSelected;

        public int Value { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                if (value) _onSelected(Value);
            }
        }

        public PriorityOptionViewModel(int value, Action<int> onSelected)
        {
            Value = value;
            _onSelected = onSelected;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Backs the Search Locations dialog. Edits a working copy of the list; nothing is written until <see cref="Save"/>.
    /// </summary>
    public class SearchLocationsViewModel : INotifyPropertyChanged
    {
        private readonly ISearchLocationStore _store;
        private readonly IUserDialogs _dialogs;
        private readonly IFolderPicker _folderPicker;
        private readonly IPathProbe _pathProbe;

        private SearchLocationItemViewModel? _editingItem;
        private bool _isEditorOpen;
        private string _editorPath = string.Empty;
        private int _editorPriority = SearchLocationRules.MinPriority;
        private bool _editorIsExternal;
        private string? _editorError;

        public ObservableCollection<SearchLocationItemViewModel> Locations { get; } = new();

        public IReadOnlyList<PriorityOptionViewModel> PriorityOptions { get; }

        public bool IsEmpty => !Locations.Any(l => !l.IsMarkedForRemoval);

        public bool HasLocations => Locations.Count > 0;

        public bool ShowEmptyState => !HasLocations && !IsEditorOpen;

        public bool HasChanges => Locations.Any(l =>
            (l.IsNew && !l.IsMarkedForRemoval) ||
            (!l.IsNew && (l.IsMarkedForRemoval || l.IsModified)));

        public bool CanSave => HasChanges && !IsEditorOpen && !HasBlockingOverlap();

        public bool IsEditorOpen
        {
            get => _isEditorOpen;
            private set
            {
                if (_isEditorOpen == value) return;
                _isEditorOpen = value;
                OnPropertyChanged(nameof(IsEditorOpen));
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(CanSave));
            }
        }

        public string EditorTitle => _editingItem is null ? "Add search location" : "Edit search location";

        public string EditorPath
        {
            get => _editorPath;
            set
            {
                if (_editorPath == value) return;
                _editorPath = value;
                EditorError = null;
                OnPropertyChanged(nameof(EditorPath));
            }
        }

        public int EditorPriority
        {
            get => _editorPriority;
            set
            {
                _editorPriority = value;
                foreach (var option in PriorityOptions)
                    option.IsSelected = option.Value == value;
                OnPropertyChanged(nameof(EditorPriority));
            }
        }

        public bool EditorIsExternal
        {
            get => _editorIsExternal;
            set
            {
                if (_editorIsExternal == value) return;
                _editorIsExternal = value;
                EditorError = null;
                OnPropertyChanged(nameof(EditorIsExternal));
            }
        }

        public string? EditorError
        {
            get => _editorError;
            private set
            {
                if (_editorError == value) return;
                _editorError = value;
                OnPropertyChanged(nameof(EditorError));
                OnPropertyChanged(nameof(HasEditorError));
            }
        }

        public bool HasEditorError => !string.IsNullOrEmpty(EditorError);

        public SearchLocationsViewModel(ISearchLocationStore store, IUserDialogs dialogs, IFolderPicker folderPicker, IPathProbe pathProbe)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
            _pathProbe = pathProbe ?? throw new ArgumentNullException(nameof(pathProbe));

            PriorityOptions = Enumerable.Range(SearchLocationRules.MinPriority, SearchLocationRules.MaxPriority)
                .Select(value => new PriorityOptionViewModel(value, selected => _editorPriority = selected))
                .ToList();
        }

        public void Load()
        {
            Locations.Clear();
            foreach (var folder in _store.GetAll())
                Locations.Add(SearchLocationItemViewModel.FromModel(folder, _pathProbe.DirectoryExists(folder.Path)));

            RefreshOverlapWarnings();
            NotifyListStateChanged();
        }

        public void BeginAdd()
        {
            string? pickedPath = _folderPicker.PickFolder("Add a search location", null);
            if (pickedPath is null) return;

            OpenEditor(null, pickedPath, SearchLocationRules.MinPriority, _pathProbe.IsRemovableDrive(pickedPath));
        }

        public void BeginEdit(SearchLocationItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(item);
            int priority = SearchLocationRules.IsValidPriority(item.Priority) ? item.Priority : SearchLocationRules.MinPriority;
            OpenEditor(item, item.Path, priority, item.IsExternal);
        }

        public void BrowseForEditorPath()
        {
            string? pickedPath = _folderPicker.PickFolder("Choose a folder", EditorPath);
            if (pickedPath is not null)
                EditorPath = pickedPath;
        }

        public bool ApplyEdit()
        {
            string? error = ValidateEditor(out string normalizedPath);
            if (error is not null)
            {
                EditorError = error;
                return false;
            }

            bool isOnline = _pathProbe.DirectoryExists(normalizedPath);
            if (_editingItem is null)
                Locations.Add(new SearchLocationItemViewModel(0, normalizedPath, EditorPriority, EditorIsExternal, isOnline));
            else
                _editingItem.Update(normalizedPath, EditorPriority, EditorIsExternal, isOnline);

            CloseEditor();
            RefreshOverlapWarnings();
            NotifyListStateChanged();
            return true;
        }

        public void CancelEdit() => CloseEditor();

        public void RequestRemove(SearchLocationItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (!item.IsNew && !_dialogs.Confirm(
                    $"Remove {item.Path} from search locations?\n\nFiles on disk are not touched.",
                    "Remove Search Location",
                    MessageBoxImage.Question))
                return;

            if (item == _editingItem)
                CloseEditor();

            // A location that was never saved simply disappears; a saved one stays visible until Save so it can be undone.
            if (item.IsNew)
                Locations.Remove(item);
            else
                item.IsMarkedForRemoval = true;

            RefreshOverlapWarnings();
            NotifyListStateChanged();
        }

        public void UndoRemove(SearchLocationItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(item);
            item.IsMarkedForRemoval = false;
            RefreshOverlapWarnings();
            NotifyListStateChanged();
        }

        public bool Save()
        {
            if (!CanSave) return false;

            var active = Locations.Where(l => !l.IsMarkedForRemoval).ToList();
            var changes = new SearchLocationChanges(
                Added: active.Where(l => l.IsNew).Select(l => l.ToModel()).ToList(),
                Updated: active.Where(l => l.IsModified).Select(l => l.ToModel()).ToList(),
                RemovedIds: Locations.Where(l => l.IsMarkedForRemoval && !l.IsNew).Select(l => l.Id).ToList());

            try
            {
                _store.SaveChanges(changes);
                return true;
            }
            catch (SqliteException ex)
            {
                _dialogs.Notify($"Your search locations couldn't be saved.\n\n{ex.Message}", "Search Locations", MessageBoxImage.Error);
                return false;
            }
        }

        public bool ConfirmDiscardChanges()
        {
            return !HasChanges || _dialogs.Confirm(
                "Discard your unsaved changes to search locations?", "Discard Changes", MessageBoxImage.Warning);
        }

        private void OpenEditor(SearchLocationItemViewModel? item, string path, int priority, bool isExternal)
        {
            _editingItem = item;
            EditorPath = path;
            EditorPriority = priority;
            EditorIsExternal = isExternal;
            EditorError = null;
            OnPropertyChanged(nameof(EditorTitle));
            IsEditorOpen = true;
        }

        private void CloseEditor()
        {
            _editingItem = null;
            EditorError = null;
            IsEditorOpen = false;
        }

        private string? ValidateEditor(out string normalizedPath)
        {
            normalizedPath = string.Empty;

            var normalization = SearchLocationRules.Normalize(EditorPath);
            if (!normalization.IsValid)
                return normalization.Error;
            normalizedPath = normalization.Path!;

            if (!SearchLocationRules.IsValidPriority(EditorPriority))
                return $"Choose a priority from {SearchLocationRules.MinPriority} to {SearchLocationRules.MaxPriority}.";

            var otherPaths = Locations
                .Where(l => !l.IsMarkedForRemoval && l != _editingItem)
                .Select(l => l.Path);
            string? conflict = SearchLocationRules.FindConflict(normalizedPath, otherPaths);
            if (conflict is not null)
                return conflict;

            // An unplugged external drive is a normal state; a missing internal folder is almost certainly a typo.
            if (!EditorIsExternal && !_pathProbe.DirectoryExists(normalizedPath))
                return "This folder doesn't exist. If it's on a drive that's unplugged, tick External / removable drive.";

            return null;
        }

        private void RefreshOverlapWarnings()
        {
            var active = Locations.Where(l => !l.IsMarkedForRemoval).ToList();
            foreach (var item in Locations)
            {
                item.OverlapWarning = item.IsMarkedForRemoval
                    ? null
                    : SearchLocationRules.FindConflict(item.Path, active.Where(other => other != item).Select(other => other.Path));
            }
        }

        // Pre-existing overlaps from older databases don't block unrelated edits; overlaps the user introduces do.
        private bool HasBlockingOverlap() =>
            Locations.Any(l => !l.IsMarkedForRemoval && l.HasOverlapWarning && (l.IsNew || l.IsModified));

        private void NotifyListStateChanged()
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasLocations));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(HasChanges));
            OnPropertyChanged(nameof(CanSave));
        }

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
