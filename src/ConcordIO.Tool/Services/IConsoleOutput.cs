namespace ConcordIO.Tool.Services;

/// <summary>
/// Abstraction for console output to enable testability and redirection.
/// </summary>
public interface IConsoleOutput
{
    /// <summary>
    /// Writes a line of text to the standard output.
    /// </summary>
    /// <param name="message">The message to write. If null or empty, writes just a newline.</param>
    void WriteLine(string? message = null);

    /// <summary>
    /// Writes a line of text to the error output.
    /// </summary>
    /// <param name="message">The error message to write. If null or empty, writes just a newline.</param>
    void WriteError(string? message = null);
}

/// <summary>
/// Default implementation that writes to Console.Out and Console.Error.
/// </summary>
public class ConsoleOutput : IConsoleOutput
{
    /// <summary>
    /// Writes a line to the standard output.
    /// </summary>
    public void WriteLine(string? message = null)
    {
        Console.WriteLine(message ?? "");
    }

    /// <summary>
    /// Writes a line to the error output.
    /// </summary>
    public void WriteError(string? message = null)
    {
        Console.Error.WriteLine(message ?? "");
    }
}
