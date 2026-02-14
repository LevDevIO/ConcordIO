namespace ConcordIO.Tool.Services;

/// <summary>
/// Result of an oasdiff command execution.
/// </summary>
public class OasDiffResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// True if breaking changes were detected (exit code 1).
    /// </summary>
    public bool Breaking { get; init; }

    /// <summary>
    /// True if the oasdiff command executed successfully (exit code 0 or 1).
    /// False indicates a tool error (exit code > 1).
    /// </summary>
    public bool Success { get; init; }
}
