namespace ConcordIO.Tool.Services;

/// <summary>
/// Abstraction for file system operations to enable testing and platform independence.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts all file system operations used by the CLI tool,
/// enabling unit tests to use mock implementations without touching the real file system.
/// </para>
/// <para>
/// The default implementation uses <see cref="System.IO"/> classes directly.
/// </para>
/// </remarks>
/// <example>
/// <para>Usage in a command:</para>
/// <code>
/// public class GenerateCommand
/// {
///     private readonly IFileSystem _fileSystem;
///     
///     public GenerateCommand(IFileSystem fileSystem)
///     {
///         _fileSystem = fileSystem;
///     }
///     
///     public async Task ExecuteAsync()
///     {
///         _fileSystem.CreateDirectory("output");
///         await _fileSystem.WriteAllTextAsync("output/file.txt", "content");
///     }
/// }
/// </code>
/// <para>Mocking in tests:</para>
/// <code>
/// var mockFileSystem = new Mock&lt;IFileSystem&gt;();
/// mockFileSystem.Setup(fs => fs.FileExists(It.IsAny&lt;string&gt;())).Returns(true);
/// var command = new GenerateCommand(mockFileSystem.Object);
/// </code>
/// </example>
public interface IFileSystem
{
	/// <summary>
	/// Creates a directory at the specified path, including any necessary parent directories.
	/// </summary>
	/// <param name="path">The absolute or relative path of the directory to create.</param>
	/// <remarks>If the directory already exists, no action is taken.</remarks>
	void CreateDirectory(string path);

	/// <summary>
	/// Writes text content to a file asynchronously, creating the file if it doesn't exist.
	/// </summary>
	/// <param name="path">The path to the file to write.</param>
	/// <param name="contents">The text content to write.</param>
	/// <returns>A task representing the asynchronous write operation.</returns>
	Task WriteAllTextAsync(string path, string contents);

	/// <summary>
	/// Determines whether the specified file exists.
	/// </summary>
	/// <param name="path">The path to the file to check.</param>
	/// <returns><c>true</c> if the file exists; otherwise, <c>false</c>.</returns>
	bool FileExists(string path);

	/// <summary>
	/// Determines whether the specified directory exists.
	/// </summary>
	/// <param name="path">The path to the directory to check.</param>
	/// <returns><c>true</c> if the directory exists; otherwise, <c>false</c>.</returns>
	bool DirectoryExists(string path);

	/// <summary>
	/// Deletes the specified directory and optionally its contents.
	/// </summary>
	/// <param name="path">The path to the directory to delete.</param>
	/// <param name="recursive">If <c>true</c>, deletes subdirectories and files; otherwise, only deletes empty directories.</param>
	void DeleteDirectory(string path, bool recursive);

	/// <summary>
	/// Gets the files in the specified directory that match the search pattern.
	/// </summary>
	/// <param name="path">The path to the directory to search.</param>
	/// <param name="searchPattern">The search pattern (e.g., "*.yaml", "*.json"). Default is "*".</param>
	/// <returns>An array of file paths matching the pattern.</returns>
	string[] GetFiles(string path, string searchPattern = "*");

	/// <summary>
	/// Gets all subdirectories in the specified directory.
	/// </summary>
	/// <param name="path">The path to the directory to search.</param>
	/// <returns>An array of directory paths.</returns>
	string[] GetDirectories(string path);

	/// <summary>
	/// Copies a file to a new location, optionally overwriting the destination.
	/// </summary>
	/// <param name="sourceFileName">The file to copy.</param>
	/// <param name="destFileName">The destination path for the copied file.</param>
	/// <param name="overwrite">If <c>true</c>, overwrites the destination file if it exists; otherwise, throws if the destination exists.</param>
	void CopyFile(string sourceFileName, string destFileName, bool overwrite = false);
}
