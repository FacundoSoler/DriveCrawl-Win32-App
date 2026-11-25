using System.IO;
using System.Reflection;

namespace DriveCrawl_Win32_App.Data
{
    /// <summary>
    /// Reads the versioned SQL scripts compiled into the assembly from Data/Migrations/NNNN_name.sql.
    /// </summary>
    public static class EmbeddedSchemaMigrations
    {
        private const string ResourcePrefix = "Migrations/";
        private const string ResourceSuffix = ".sql";

        public static IReadOnlyList<SchemaMigration> Load()
        {
            var assembly = typeof(EmbeddedSchemaMigrations).Assembly;

            return assembly.GetManifestResourceNames()
                .Where(IsMigrationResource)
                .Select(name => ReadMigration(assembly, name))
                .OrderBy(m => m.Version)
                .ToList();
        }

        private static bool IsMigrationResource(string resourceName) =>
            resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
            resourceName.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase);

        private static SchemaMigration ReadMigration(Assembly assembly, string resourceName)
        {
            string fileName = resourceName[ResourcePrefix.Length..^ResourceSuffix.Length];
            int separatorIndex = fileName.IndexOf('_');

            if (separatorIndex <= 0 || !int.TryParse(fileName[..separatorIndex], out int version))
                throw new InvalidOperationException($"Migration '{resourceName}' must be named NNNN_description.sql.");

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Migration resource '{resourceName}' could not be opened.");
            using var reader = new StreamReader(stream);

            return new SchemaMigration(version, fileName[(separatorIndex + 1)..], reader.ReadToEnd());
        }
    }
}
