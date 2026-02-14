namespace ConcordIO.Tool.Services;

/// <summary>
/// Abstraction for console output to enable testability and output redirection.
/// </summary>
/// <remarks>
/// <para>
/// This interface allows CLI commands to write output without directly depending on
/// <see cref="Console"/>, making them easier to test and allowing output capture.
/// </para>
/// <para>
/// The default implementation <see cref="ConsoleOutput"/> writes to <see cref="Console.Out"/>
/// and <see cref="Console.Error"/>.
/// </para>
/// </remarks>
/// <example>
/// <para>Usage in a command:</para>
/// <code>
/// public class MyCommand
/// {
///     private readonly IConsoleOutput _console;
///     
///     public MyCommand(IConsoleOutput console)
///     {
///         _console = console;
///     }
///     
///     public void Execute()
///     {
///         _console.WriteLine("Processing...");
///         _console.WriteError("Warning: something happened");
///     }
/// }
/// </code>
/// <para>Capturing output in tests:</para>
/// <code>
/// var output = new StringBuilder();
/// var mockConsole = new Mock&lt;IConsoleOutput&gt;();
/// mockConsole.Setup(c => c.WriteLine(It.IsAny&lt;string&gt;()))
///     .Callback&lt;string&gt;(s => output.AppendLine(s));
/// </code>
/// </example>
public interface IConsoleOutput
{
	/// <summary>
	/// Writes a line of text to the standard output stream.
	/// </summary>
	/// <param name="message">The message to write. If <c>null</c> or empty, writes only a newline.</param>
	/// <example>
	/// <code>
	/// console.WriteLine("Operation completed successfully.");
	/// console.WriteLine();  // Empty line
	/// </code>
	/// </example>
	void WriteLine(string? message = null);

	/// <summary>
	/// Writes a line of text to the standard error stream.
	/// </summary>
	/// <param name="message">The error message to write. If <c>null</c> or empty, writes only a newline.</param>
	/// <example>
	/// <code>
	/// console.WriteError("Error: File not found.");
	/// console.WriteError("Warning: Deprecated option used.");
	/// </code>
	/// </example>
	void WriteError(string? message = null);
}

/// <summary>
/// Default implementation of <see cref="IConsoleOutput"/> that writes to <see cref="Console.Out"/> and <see cref="Console.Error"/>.
/// </summary>
/// <remarks>
/// This is the production implementation used by the CLI tool.
/// For testing, use a mock or a custom implementation that captures output.
/// </remarks>
public class ConsoleOutput : IConsoleOutput
{
	/// <summary>
	/// Writes a line to <see cref="Console.Out"/>.
	/// </summary>
	/// <param name="message">The message to write.</param>
	public void WriteLine(string? message = null)
	{
		Console.WriteLine(message ?? "");
	}

	/// <summary>
	/// Writes a line to <see cref="Console.Error"/>.
	/// </summary>
	/// <param name="message">The error message to write.</param>
	public void WriteError(string? message = null)
	{
		Console.Error.WriteLine(message ?? "");
	}
}
