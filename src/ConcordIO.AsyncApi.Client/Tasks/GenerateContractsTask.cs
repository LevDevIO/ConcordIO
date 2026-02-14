using System.Text.Json;

using ConcordIO.AsyncApi.Client;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Neuroglia.AsyncApi.v3;
using Neuroglia.Serialization.Yaml;

using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace ConcordIO.AsyncApi.Client.Tasks;

/// <summary>
/// MSBuild task that generates C# contract types from AsyncAPI specifications.
/// </summary>
public class GenerateContractsTask : MSBuildTask
{
	/// <summary>
	/// The AsyncAPI specification files to process.
	/// Each item should be a path to a YAML or JSON AsyncAPI file.
	/// </summary>
	[Required]
	public ITaskItem[] AsyncApiFiles { get; set; } = [];

	/// <summary>
	/// The output directory for generated C# files.
	/// </summary>
	[Required]
	public string OutputDirectory { get; set; } = string.Empty;

	/// <summary>
	/// Referenced assembly paths for external type detection.
	/// </summary>
	public ITaskItem[] ReferencedAssemblies { get; set; } = [];

	/// <summary>
	/// Whether to generate System.ComponentModel.DataAnnotations attributes.
	/// </summary>
	public bool GenerateDataAnnotations { get; set; } = true;

	/// <summary>
	/// Whether to generate nullable reference type annotations.
	/// </summary>
	public bool GenerateNullableReferenceTypes { get; set; } = true;

	/// <summary>
	/// The class style to use: "Poco" or "Record".
	/// </summary>
	public string ClassStyle { get; set; } = "Poco";

	/// <summary>
	/// The paths to the generated files (output parameter).
	/// </summary>
	[Output]
	public ITaskItem[] GeneratedFiles { get; set; } = [];

	public override bool Execute()
	{
		try
		{
			if (AsyncApiFiles.Length == 0)
			{
				Log.LogMessage(MessageImportance.Normal, "ConcordIO.Client: No AsyncAPI files specified, skipping generation.");
				return true;
			}

			Log.LogMessage(MessageImportance.High, "ConcordIO.Client: Generating contracts from {0} AsyncAPI file(s)...", AsyncApiFiles.Length);

			// Ensure output directory exists
			Directory.CreateDirectory(OutputDirectory);

			// Set up external type resolver with referenced assemblies
			using var resolver = new ExternalTypeResolver();
			var assemblyPaths = ReferencedAssemblies.Select(r => r.ItemSpec).Where(File.Exists);
			resolver.LoadAssemblies(assemblyPaths);

			// Configure generator settings
			var settings = new ContractGeneratorSettings(
				GenerateDataAnnotations: GenerateDataAnnotations,
				GenerateNullableReferenceTypes: GenerateNullableReferenceTypes,
				ClassStyle: ClassStyle.Equals("Record", StringComparison.OrdinalIgnoreCase)
					? GeneratedClassStyle.Record
					: GeneratedClassStyle.Poco
			);

			var generator = new AsyncApiContractGenerator(settings, resolver);
			var generatedFiles = new List<ITaskItem>();

			foreach (var asyncApiFile in AsyncApiFiles)
			{
				var filePath = asyncApiFile.ItemSpec;

				if (!File.Exists(filePath))
				{
					Log.LogWarning("ConcordIO.Client: AsyncAPI file not found: {0}", filePath);
					continue;
				}

				Log.LogMessage(MessageImportance.Normal, "ConcordIO.Client: Processing {0}", filePath);

				try
				{
					// Load and parse the AsyncAPI document
					var document = LoadAsyncApiDocument(filePath);

					// Generate contracts
					var result = generator.Generate(document);

					// Write generated files
					foreach (var sourceFile in result.SourceFiles)
					{
						var outputPath = Path.Combine(OutputDirectory, sourceFile.FileName);
						File.WriteAllText(outputPath, sourceFile.Content);

						generatedFiles.Add(new TaskItem(outputPath));
						Log.LogMessage(MessageImportance.Normal, "ConcordIO.Client: Generated {0} ({1} types)",
							sourceFile.FileName, sourceFile.Types.Count);
					}

					// Log external types
					if (result.ExternalTypes.Count > 0)
					{
						Log.LogMessage(MessageImportance.Normal,
							"ConcordIO.Client: Skipped {0} external type(s) already defined in referenced assemblies.",
							result.ExternalTypes.Count);
					}

					Log.LogMessage(MessageImportance.High,
						"ConcordIO.Client: Generated {0} type(s) from {1}",
						result.GeneratedTypes.Count, Path.GetFileName(filePath));
				}
				catch (Exception ex)
				{
					Log.LogError("ConcordIO.Client: Error processing {0}: {1}", filePath, ex.Message);
					return false;
				}
			}

			GeneratedFiles = generatedFiles.ToArray();
			return true;
		}
		catch (Exception ex)
		{
			Log.LogErrorFromException(ex, showStackTrace: true);
			return false;
		}
	}

	private static V3AsyncApiDocument LoadAsyncApiDocument(string filePath)
	{
		var content = File.ReadAllText(filePath);
		var extension = Path.GetExtension(filePath).ToLowerInvariant();

		V3AsyncApiDocument document;

		if (extension == ".yaml" || extension == ".yml")
		{
			// Parse YAML using Neuroglia's default serializer
			document = YamlSerializer.Default.Deserialize<V3AsyncApiDocument>(content)
				?? throw new InvalidOperationException($"Failed to parse AsyncAPI YAML file: {filePath}");
		}
		else
		{
			// Parse JSON
			document = JsonSerializer.Deserialize<V3AsyncApiDocument>(content)
				?? throw new InvalidOperationException($"Failed to parse AsyncAPI JSON file: {filePath}");
		}

		return document;
	}
}
