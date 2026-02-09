namespace ConcordIO.Tool.Services;

/// <summary>
/// Manages temporary directory lifecycle - creation, usage, and cleanup.
/// Automatically cleans up directory on dispose if it was created by this scope.
/// </summary>
public class TempDirectoryScope : IAsyncDisposable
{
    private readonly string _path;
    private readonly bool _shouldCleanup;

    /// <summary>
    /// Gets the path to the temporary directory.
    /// </summary>
    public string Path => _path;

    /// <summary>
    /// Creates a TempDirectoryScope. If workingDirectory is null, creates a new temp directory.
    /// If workingDirectory is provided, uses that directory without auto-cleanup.
    /// </summary>
    /// <param name="workingDirectory">Optional working directory path. If null, a temp directory is created.</param>
    public TempDirectoryScope(string? workingDirectory)
    {
        _shouldCleanup = workingDirectory == null;
        if (_shouldCleanup)
        {
            _path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ConcordIO",
                System.IO.Path.GetRandomFileName().Replace(".", ""));
            Directory.CreateDirectory(_path);
        }
        else
        {
            _path = workingDirectory!;
        }
    }

    /// <summary>
    /// Disposes the scope and cleans up the temporary directory if it was created by this scope.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_shouldCleanup)
        {
            try
            {
                Directory.Delete(_path, true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to clean up temp directory '{_path}': {ex.Message}");
            }
        }

        await ValueTask.CompletedTask;
    }
}
