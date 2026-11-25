using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DriveCrawl_Win32_App.Models;

namespace DriveCrawl_Win32_App.Services
{
    public record AppSettings(DataLocation DataLocation);

    /// <summary>
    /// Persists <see cref="AppSettings"/> as a small human-readable JSON file.
    /// </summary>
    public class AppSettingsStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _filePath;

        public AppSettingsStore(string filePath)
        {
            _filePath = filePath;
        }

        public AppSettings? Load()
        {
            if (!File.Exists(_filePath))
                return null;

            try
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), SerializerOptions);
                return settings is not null && Enum.IsDefined(settings.DataLocation) ? settings : null;
            }
            catch (JsonException)
            {
                // A hand-edited or truncated file is treated as "no choice made yet" so the user is simply asked again.
                return null;
            }
        }

        public void Save(AppSettings settings)
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, SerializerOptions));
        }
    }
}
