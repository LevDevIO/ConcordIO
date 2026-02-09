namespace ConcordIO.Tool.Services;

/// <summary>
/// Manages temporary directory lifecycle - creation, usage, and cleanup.
/// Automatically cleans up directory on dispose if it was created by this scope.
/// </summary>
public class TempDirectoryScope : IDisposable
{
    private readonly string _path;
    private readonly bool _shouldCleanup;
    private readonly IConsoleOutput? _console;

    /// <summary>
    /// Gets the path to the temporary directory.
    /// </summary>
    public string Path => _path;

    /// <summary>
    /// Creates a TempDirectoryScope. If workingDirectory is null, creates a new temp directory.
    /// If workingDirectory is provided, uses that directory without auto-cleanup.
    /// </summary>
    /// <param name="workingDirectory">Optional working directory path. If null, a temp directory is created.</param>
    /// <param name="console">Optional console output for error reporting during cleanup.</param>
    public TempDirectoryScope(string? workingDirectory, IConsoleOutput? console = null)
    {
        _console = console;
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
    public void Dispose()
    {
        if (_shouldCleanup)
        {
            try
            {
                Directory.Delete(_path, true);
            }
            catch (Exception ex)
            {
                _console?.WriteError($"Failed to clean up temp directory '{_path}': {ex.Message}");
            }
        }

    }
}
