using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

using ConcordIO.AsyncApi.Server;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

[assembly: InternalsVisibleTo("ConcordIO.AsyncApi.Tests")]

namespace ConcordIO.AsyncApi.Server.Tasks;

/// <summary>
/// MSBuild task that generates an AsyncAPI specification from .NET message types.
/// </summary>
public class GenerateAsyncApiTask : Microsoft.Build.Utilities.Task
{
	/// <summary>
	/// The path to the compiled assembly containing message types.
	/// </summary>
	[Required]
	public string AssemblyPath { get; set; } = string.Empty;

	/// <summary>
	/// The message type patterns to discover. Each item should have Include (pattern) and Kind metadata.
	/// </summary>
	public ITaskItem[] MessageTypePatterns { get; set; } = [];

	/// <summary>
	/// The title for the AsyncAPI document. Defaults to the assembly name.
	/// </summary>
	public string DocumentTitle { get; set; } = string.Empty;

	/// <summary>
	/// The version for the AsyncAPI document. Defaults to "1.0.0".
	/// </summary>
	public string DocumentVersion { get; set; } = string.Empty;

	/// <summary>
	/// The output file path for the generated AsyncAPI specification.
	/// If not specified, defaults to the assembly directory with .yaml extension.
	/// </summary>
	public string OutputPath { get; set; } = string.Empty;

	/// <summary>
	/// The output format: "yaml" or "json". Defaults to "yaml".
	/// </summary>
	public string OutputFormat { get; set; } = "yaml";

	/// <summary>
	/// The path to the generated AsyncAPI file (output parameter).
	/// </summary>
	[Output]
	public string GeneratedFile { get; set; } = string.Empty;

	public override bool Execute()
	{
		try
		{
			Log.LogMessage(MessageImportance.Normal, "Loading assembly: {0}", AssemblyPath);

			if (!File.Exists(AssemblyPath))
			{
				Log.LogError("Assembly not found: {0}", AssemblyPath);
				return false;
			}

			// Use a collectible AssemblyLoadContext to prevent memory leaks in long-running MSBuild processes.
			// The custom context resolves dependencies from the assembly's output directory, replacing
			// the old AppDomain.CurrentDomain.AssemblyResolve handler approach.
			var assemblyDir = Path.GetDirectoryName(AssemblyPath) ?? ".";
			var alc = new ConcordIOAssemblyLoadContext(assemblyDir);
			try
			{
				var assembly = alc.LoadFromAssemblyPath(AssemblyPath);

				// Load referenced assemblies for type discovery
				var assemblies = GetSearchableAssemblies(alc, assembly, assemblyDir);

				Log.LogMessage(MessageImportance.Normal,
					"ConcordIO: Scanning {0} assemblies for message types: {1}",
					assemblies.Count,
					string.Join(", ", assemblies.Select(a => a.GetName().Name)));

				// Apply defaults for optional parameters
				var assemblyName = assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(AssemblyPath);
				var title = string.IsNullOrWhiteSpace(DocumentTitle) ? assemblyName : DocumentTitle;
				var version = string.IsNullOrWhiteSpace(DocumentVersion) ? "1.0.0" : DocumentVersion;
				var isJson = OutputFormat.Equals("json", StringComparison.OrdinalIgnoreCase);
				var extension = isJson ? ".json" : ".yaml";
				var outputPath = string.IsNullOrWhiteSpace(OutputPath)
					? Path.Combine(Path.GetDirectoryName(AssemblyPath) ?? ".", $"{assemblyName}{extension}")
					: OutputPath;

				// Parse patterns from MSBuild items
				var patterns = ParsePatterns();
				if (patterns.Count == 0)
				{
					Log.LogWarning("No message type patterns specified.");
					return true;
				}

				Log.LogMessage(MessageImportance.Normal, "Discovering types with {0} patterns...", patterns.Count);

				// Discover types
				var discoveryService = new TypeDiscoveryService();
				var discoveredTypes = discoveryService.DiscoverTypes(assemblies, patterns).ToList();

				if (discoveredTypes.Count == 0)
				{
					Log.LogWarning("No message types found matching the specified patterns.");
					return true;
				}

				Log.LogMessage(MessageImportance.High, "Found {0} message types.", discoveredTypes.Count);
				foreach (var dt in discoveredTypes)
				{
					Log.LogMessage(MessageImportance.Normal, "  - {0} ({1})", dt.Type.FullName, dt.Kind);
				}

				// Generate AsyncAPI document
				var generator = new AsyncApiDocumentGenerator();
				var document = generator.Generate(title, version, discoveredTypes);

				// Write to file
				var writer = new AsyncApiDocumentWriter();

				if (isJson)
				{
					writer.WriteJson(document, outputPath);
				}
				else
				{
					writer.WriteYaml(document, outputPath);
				}

				GeneratedFile = outputPath;
				Log.LogMessage(MessageImportance.High, "Generated AsyncAPI specification: {0}", outputPath);

				return true;
			}
			finally
			{
				alc.Unload();
			}
		}
		catch (Exception ex)
		{
			Log.LogErrorFromException(ex, showStackTrace: true);
			return false;
		}
	}

	private List<MessageTypePattern> ParsePatterns()
	{
		var patterns = new List<MessageTypePattern>();

		foreach (var item in MessageTypePatterns)
		{
			var pattern = item.ItemSpec;
			var kindString = item.GetMetadata("Kind");

			var kind = kindString?.Equals("Command", StringComparison.OrdinalIgnoreCase) == true
				? MessageKind.Command
				: MessageKind.Event;

			patterns.Add(new MessageTypePattern(pattern, kind));
			Log.LogMessage(MessageImportance.Low, "Pattern: {0} ({1})", pattern, kind);
		}

		return patterns;
	}

	/// <summary>
	/// Gets the list of assemblies to search for message types.
	/// Includes the primary assembly and its referenced assemblies recursively (excluding framework assemblies).
	/// </summary>
	/// <param name="alc">The AssemblyLoadContext for loading assemblies.</param>
	/// <param name="primary">The primary assembly (from TargetPath).</param>
	/// <param name="probeDir">The directory to probe for referenced assemblies.</param>
	/// <returns>List of assemblies to scan for message types.</returns>
	/// <remarks>
	/// This method recursively loads referenced assemblies to support transitive dependencies
	/// (e.g., A→B→C→D). Framework assemblies are filtered out to avoid loading the entire
	/// dependency graph. A visited set prevents duplicate loading and infinite loops.
	/// </remarks>
	private List<Assembly> GetSearchableAssemblies(
		AssemblyLoadContext alc,
		Assembly primary,
		string probeDir)
	{
		var result = new List<Assembly>();
		var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// Recursively load assemblies starting from the primary
		LoadAssemblyAndReferences(alc, primary, probeDir, result, visited, depth: 0);

		return result;
	}

	/// <summary>
	/// Recursively loads an assembly and its non-framework references.
	/// </summary>
	/// <param name="alc">The AssemblyLoadContext for loading assemblies.</param>
	/// <param name="assembly">The assembly to process.</param>
	/// <param name="probeDir">The directory to probe for referenced assemblies.</param>
	/// <param name="result">The list to add discovered assemblies to.</param>
	/// <param name="visited">Set of already-visited assembly names to prevent duplicates.</param>
	/// <param name="depth">Current recursion depth (for logging).</param>
	private void LoadAssemblyAndReferences(
		AssemblyLoadContext alc,
		Assembly assembly,
		string probeDir,
		List<Assembly> result,
		HashSet<string> visited,
		int depth)
	{
		var assemblyName = assembly.GetName().Name;
		if (assemblyName == null || !visited.Add(assemblyName))
		{
			return; // Already processed or null name
		}

		// Add this assembly to results
		result.Add(assembly);
		var indent = new string(' ', depth * 2);
		Log.LogMessage(MessageImportance.Low,
			"{0}Loaded assembly: {1}", indent, assemblyName);

		// Recursively process references
		foreach (var refName in assembly.GetReferencedAssemblies())
		{
			// Skip framework/runtime assemblies — they won't contain user message types
			if (IsFrameworkAssembly(refName.Name))
			{
				continue;
			}

			// Skip if already visited - this check avoids unnecessary file system probes
			// for assemblies that have already been processed (common in diamond dependencies)
			if (refName.Name != null && visited.Contains(refName.Name))
			{
				continue;
			}

			// Try both .dll and .exe extensions (assemblies can have either)
			var possiblePaths = new[]
			{
				Path.Combine(probeDir, refName.Name + ".dll"),
				Path.Combine(probeDir, refName.Name + ".exe")
			};

			foreach (var path in possiblePaths)
			{
				if (File.Exists(path))
				{
					try
					{
						var refAssembly = alc.LoadFromAssemblyPath(path);
						// Recursively load this assembly's references
						LoadAssemblyAndReferences(alc, refAssembly, probeDir, result, visited, depth + 1);
						break; // Found and loaded, no need to try other extensions
					}
					catch (Exception ex)
					{
						// Skip assemblies that fail to load
						Log.LogMessage(MessageImportance.Low,
							"{0}Skipped assembly {1}: {2}", indent, refName.Name, ex.Message);
					}
				}
			}
		}
	}

	/// <summary>
	/// Assembly name prefixes for framework and third-party libraries that should be excluded
	/// from message type discovery. These assemblies are part of the .NET runtime, ConcordIO's
	/// own dependencies, or common infrastructure libraries that won't contain user message types.
	/// </summary>
	/// <remarks>
	/// This list includes:
	/// <list type="bullet">
	/// <item><description>.NET runtime assemblies (System, Microsoft, mscorlib, netstandard)</description></item>
	/// <item><description>ConcordIO's AsyncAPI dependencies (Neuroglia, NJsonSchema)</description></item>
	/// <item><description>Common serialization libraries (Newtonsoft)</description></item>
	/// </list>
	/// If your message types are in an assembly that starts with one of these prefixes,
	/// you'll need to explicitly include it using a different pattern or rename the assembly.
	/// </remarks>
	private static readonly string[] FrameworkAssemblyPrefixes =
	[
		"System",
		"Microsoft",
		"netstandard",
		"mscorlib",
		"Neuroglia",      // ConcordIO AsyncAPI dependency
		"NJsonSchema",    // ConcordIO schema generation dependency
		"Newtonsoft",     // JSON serialization library
	];

	/// <summary>
	/// Determines if an assembly is a framework/runtime assembly that shouldn't be scanned for message types.
	/// </summary>
	/// <param name="name">The assembly name.</param>
	/// <returns>True if this is a framework assembly that should be skipped.</returns>
	private static bool IsFrameworkAssembly(string? name) =>
		name is null ||
		FrameworkAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
}

/// <summary>
/// Collectible AssemblyLoadContext that resolves dependencies from a base directory.
/// This replaces the old AppDomain.CurrentDomain.AssemblyResolve approach, providing
/// the same dependency resolution without the memory leak.
/// </summary>
internal sealed class ConcordIOAssemblyLoadContext(string basePath)
	: AssemblyLoadContext("ConcordIO-GenerateAsyncApi", isCollectible: true)
{
	protected override Assembly? Load(AssemblyName assemblyName)
	{
		var path = Path.Combine(basePath, assemblyName.Name + ".dll");
		return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
	}
}
