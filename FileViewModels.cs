using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DriveCrawl_Win32_App.Models;

namespace DriveCrawl_Win32_App
{
    public class FileViewModel : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string FullPath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string Extension { get; set; } = "";
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Priority { get; set; }

        public TimeSpan? Duration { get; set; }
        public int? BitrateKbps { get; set; }

        private ImageSource? _thumbnail;
        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static FileViewModel FromModel(FileModel m) => new FileViewModel
        {
            Id = m.Id,
            FullPath = m.FullPath,
            FileName = m.FileName,
            Extension = m.Extension,
            FileSize = m.FileSize,
            CreatedAt = m.CreatedAt,
            Priority = m.Priority,
            Duration = m.Duration,
            BitrateKbps = m.BitrateKbps
        };
    }

    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long b)
                return b switch
                {
                    >= 1_073_741_824 => $"{b / 1_073_741_824.0:F1} GB",
                    >= 1_048_576 => $"{b / 1_048_576.0:F1} MB",
                    >= 1_024 => $"{b / 1_024.0:F0} KB",
                    _ => $"{b} B"
                };
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
