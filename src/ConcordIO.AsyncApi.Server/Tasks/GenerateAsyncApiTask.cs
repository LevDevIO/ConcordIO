using ConcordIO.AsyncApi.Server;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Reflection;

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

            // Set up assembly resolution for dependencies
            var assemblyDir = Path.GetDirectoryName(AssemblyPath) ?? ".";
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var assemblyName = new AssemblyName(args.Name);
                var assemblyFilePath = Path.Combine(assemblyDir, assemblyName.Name + ".dll");
                if (File.Exists(assemblyFilePath))
                {
                    return Assembly.LoadFrom(assemblyFilePath);
                }
                return null;
            };

            var assembly = Assembly.LoadFrom(AssemblyPath);

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
            var discoveredTypes = discoveryService.DiscoverTypes(assembly, patterns).ToList();

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
                writer.WriteJsonAsync(document, outputPath).GetAwaiter().GetResult();
            }
            else
            {
                writer.WriteYamlAsync(document, outputPath).GetAwaiter().GetResult();
            }

            GeneratedFile = outputPath;
            Log.LogMessage(MessageImportance.High, "Generated AsyncAPI specification: {0}", outputPath);

            return true;
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
}
