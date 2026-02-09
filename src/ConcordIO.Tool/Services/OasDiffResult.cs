namespace ConcordIO.Tool.Services;

/// <summary>
/// Result of an oasdiff command execution.
/// </summary>
public class OasDiffResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public bool Breaking { get; init; }
}
