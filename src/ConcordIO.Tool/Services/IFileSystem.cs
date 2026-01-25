namespace ConcordIO.Tool.Services;

/// <summary>
/// Abstraction for file system operations to enable testing.
/// </summary>
public interface IFileSystem
{
    void CreateDirectory(string path);
    Task WriteAllTextAsync(string path, string contents);
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void DeleteDirectory(string path, bool recursive);
    string[] GetFiles(string path, string searchPattern = "*");
    string[] GetDirectories(string path);
}
