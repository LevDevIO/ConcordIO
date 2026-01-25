namespace ConcordIO.Tool.Services;

/// <summary>
/// Default implementation of <see cref="IFileSystem"/> that wraps System.IO operations.
/// </summary>
public class FileSystem : IFileSystem
{
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public Task WriteAllTextAsync(string path, string contents) => File.WriteAllTextAsync(path, contents);

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    public string[] GetFiles(string path, string searchPattern = "*") => Directory.GetFiles(path, searchPattern);

    public string[] GetDirectories(string path) => Directory.GetDirectories(path);
}
