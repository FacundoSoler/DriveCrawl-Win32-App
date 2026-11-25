using System.ComponentModel;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;

namespace DriveCrawl_Win32_App.ViewModels
{
    public enum DataLocationPromptMode
    {
        FirstRun,
        Change
    }

    public class DataLocationOptionViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public required DataLocation Location { get; init; }
        public required string Title { get; init; }
        public required string Description { get; init; }
        public required string DatabasePath { get; init; }
        public required bool IsAvailable { get; init; }
        public string Badge { get; init; } = string.Empty;

        public bool HasBadge => !string.IsNullOrEmpty(Badge);

        public bool IsUnavailable => !IsAvailable;

        public string UnavailableReason => IsAvailable ? string.Empty : "Not available: DriveCrawl can't write to this folder.";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Backs the "where should DriveCrawl keep your library?" dialog, both on first run and when changing location later.
    /// </summary>
    public class DataLocationViewModel : INotifyPropertyChanged
    {
        private readonly DataLocationPromptMode _mode;
        private readonly DataLocation? _currentLocation;
        private bool _copyCurrentLibrary = true;

        public IReadOnlyList<DataLocationOptionViewModel> Options { get; }

        public string Heading => _mode == DataLocationPromptMode.FirstRun
            ? "Where should DriveCrawl keep your library?"
            : "Change library location";

        public string Subheading => _mode == DataLocationPromptMode.FirstRun
            ? "Your library stores the folders DriveCrawl searches. You can change this later from the Library button in the status bar."
            : "DriveCrawl will restart to start using the new location.";

        public string ConfirmText => _mode == DataLocationPromptMode.FirstRun ? "Continue" : "Change and restart";

        public string CancelText => _mode == DataLocationPromptMode.FirstRun ? "Quit" : "Cancel";

        public bool ShowCopyOption { get; }

        public bool CopyCurrentLibrary
        {
            get => _copyCurrentLibrary;
            set
            {
                if (_copyCurrentLibrary == value) return;
                _copyCurrentLibrary = value;
                OnPropertyChanged(nameof(CopyCurrentLibrary));
            }
        }

        public DataLocation? SelectedLocation => Options.FirstOrDefault(o => o.IsSelected)?.Location;

        public bool CanConfirm => SelectedLocation is { } selected &&
                                  Options.Single(o => o.Location == selected).IsAvailable &&
                                  (_mode == DataLocationPromptMode.FirstRun || selected != _currentLocation);

        public DataLocationViewModel(DataLocationService service, DataLocationPromptMode mode)
        {
            ArgumentNullException.ThrowIfNull(service);
            _mode = mode;
            _currentLocation = service.CurrentLocation;

            Options = new[]
            {
                CreateOption(service, DataLocation.AppFolder,
                    "Next to DriveCrawl (portable)",
                    "Keeps your library in a Database folder beside DriveCrawl.exe. Easy to find, back up, or carry on a USB drive."),
                CreateOption(service, DataLocation.UserProfile,
                    "In your Windows user profile",
                    "The standard Windows place for app data (AppData\\Local). Private to your account, and works even when DriveCrawl is installed under Program Files."),
            };

            ShowCopyOption = mode == DataLocationPromptMode.Change &&
                             _currentLocation is { } current && service.HasLibraryAt(current);

            SelectInitialOption();

            foreach (var option in Options)
                option.PropertyChanged += OnOptionPropertyChanged;
        }

        private DataLocationOptionViewModel CreateOption(DataLocationService service, DataLocation location, string title, string description)
        {
            return new DataLocationOptionViewModel
            {
                Location = location,
                Title = title,
                Description = description,
                DatabasePath = service.GetDatabasePath(location),
                IsAvailable = service.IsAvailable(location),
                Badge = BadgeFor(location)
            };
        }

        private string BadgeFor(DataLocation location)
        {
            if (_mode == DataLocationPromptMode.Change)
                return location == _currentLocation ? "Current" : string.Empty;

            return location == DataLocation.AppFolder ? "Default" : string.Empty;
        }

        private void SelectInitialOption()
        {
            var preferred = _mode == DataLocationPromptMode.Change && _currentLocation is { } current
                ? Options.Single(o => o.Location == current)
                : Options.FirstOrDefault(o => o.IsAvailable);

            if (preferred is not null)
                preferred.IsSelected = true;
        }

        private void OnOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DataLocationOptionViewModel.IsSelected)) return;

            OnPropertyChanged(nameof(SelectedLocation));
            OnPropertyChanged(nameof(CanConfirm));
        }

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
