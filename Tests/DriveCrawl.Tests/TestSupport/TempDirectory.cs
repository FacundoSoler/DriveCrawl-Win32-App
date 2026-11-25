namespace DriveCrawl.Tests.TestSupport;

/// <summary>
/// A unique, self-cleaning directory under %TEMP% so file-system tests never touch real user data.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DriveCrawl.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string CreateFile(string relativePath, int sizeInBytes = 0)
    {
        string fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, new byte[sizeInBytes]);
        return fullPath;
    }

    public string CreateSubdirectory(string relativePath)
    {
        string fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leaked temp folder must never fail a test run.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
