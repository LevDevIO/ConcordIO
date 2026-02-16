namespace ConcordIO.Tool.Services;

/// <summary>
/// Helper methods for parsing and working with API specification file entries.
/// </summary>
public static class SpecHelpers
{
	/// <summary>
	/// A specification entry with file path, file name, and kind.
	/// </summary>
	/// <remarks>
	/// Used to represent parsed spec file arguments with their full path information.
	/// The <c>FilePath</c> is the full absolute path, while <c>FileName</c> is just the file name.
	/// </remarks>
	/// <param name="FilePath">The full absolute path to the spec file.</param>
	/// <param name="FileName">The file name without directory path.</param>
	/// <param name="Kind">The spec kind (openapi, proto, or asyncapi).</param>
	public record SpecEntry(string FilePath, string FileName, string Kind);

	/// <summary>
	/// Valid specification kinds supported by ConcordIO.
	/// </summary>
	private static readonly IReadOnlyList<string> ValidKinds = SpecKind.All;

	/// <summary>
	/// Parses specification file arguments into spec entries with full path information.
	/// </summary>
	/// <param name="specArgs">Array of spec arguments in format "path[:kind]" where kind defaults to openapi.</param>
	/// <returns>A list of parsed spec entries with full path, filename, and kind information.</returns>
	/// <remarks>
	/// <para>
	/// This method parses spec file arguments which can be in one of two formats:
	/// </para>
	/// <list type="bullet">
	/// <item><description><c>path</c> - Uses default kind (openapi)</description></item>
	/// <item><description><c>path:kind</c> - Explicitly specifies the kind</description></item>
	/// </list>
	/// <para>
	/// The parser is Windows-aware and correctly handles paths like <c>C:\path\to\file.yaml</c>
	/// by checking if the colon is part of a Windows drive letter (position 1).
	/// </para>
	/// <para>
	/// All file paths are converted to absolute paths using <see cref="Path.GetFullPath(string)"/>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <para>Parse various spec formats:</para>
	/// <code>
	/// var entries = SpecHelpers.ParseSpecEntries([
	///     "api.yaml",                    // => openapi (default)
	///     "events.yaml:asyncapi",        // => asyncapi (explicit)
	///     "service.proto:proto",         // => proto (explicit)
	///     "C:\\specs\\api.yaml:openapi"  // => openapi (Windows path)
	/// ]);
	/// </code>
	/// </example>
	public static List<SpecEntry> ParseSpecEntries(string[] specArgs)
	{
		var entries = new List<SpecEntry>();

		foreach (var spec in specArgs)
		{
			var colonIndex = spec.LastIndexOf(':');

			// Check if colon is part of a Windows path (e.g., C:\path)
			if (colonIndex > 1 && spec.Length > colonIndex + 1)
			{
				var possibleKind = spec[(colonIndex + 1)..].ToLowerInvariant();
				if (ValidKinds.Contains(possibleKind))
				{
					var filePath = spec[..colonIndex];
					entries.Add(new SpecEntry(Path.GetFullPath(filePath), Path.GetFileName(filePath), possibleKind));
					continue;
				}
			}

			// No valid kind suffix, default to openapi
			var fullPath = Path.GetFullPath(spec);
			entries.Add(new SpecEntry(fullPath, Path.GetFileName(spec), SpecKind.OpenApi));
		}

		return entries;
	}
}
