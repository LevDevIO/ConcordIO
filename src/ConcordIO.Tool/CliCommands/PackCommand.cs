using ConcordIO.Tool.Services;

using DotMake.CommandLine;

namespace ConcordIO.Tool.CliCommands;

public partial class RootCommand
{
	/// <summary>
	/// CLI command that generates and packs contract NuGet packages (.nupkg) from API specifications.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This command combines the functionality of the <c>generate</c> command with NuGet packaging,
	/// producing ready-to-publish .nupkg files. It:
	/// </para>
	/// <list type="number">
	/// <item><description>Copies spec files to the output directory organized by kind</description></item>
	/// <item><description>Generates .nuspec and .targets files using templates</description></item>
	/// <item><description>Calls <c>nuget pack</c> to create the .nupkg file(s)</description></item>
	/// </list>
	/// <para>
	/// The generated packages follow the ConcordIO contract package structure with specs
	/// in kind-specific folders (openapi/, proto/, asyncapi/) and MSBuild integration.
	/// </para>
	/// </remarks>
	/// <example>
	/// <para>Pack a single OpenAPI spec:</para>
	/// <code>
	/// concordio pack --spec petstore.yaml --package-id MyCompany.PetStore.Contracts --version 1.0.0
	/// </code>
	/// <para>Pack with client package:</para>
	/// <code>
	/// concordio pack --spec api.yaml --package-id MyApi.Contracts --version 2.0.0 --client
	/// </code>
	/// <para>Pack multiple specs of different kinds:</para>
	/// <code>
	/// concordio pack --spec api.yaml:openapi --spec events.yaml:asyncapi --package-id MyService.Contracts --version 1.0.0
	/// </code>
	/// </example>
	[CliCommand(Name = "pack", Description = "Generate and pack contract NuGet packages (.nupkg) from OpenAPI, Protobuf, or AsyncAPI specifications")]
	public class PackCommand
	{
		private ITemplateRenderer? _templateRenderer;
		private IFileSystem? _fileSystem;
		private IConsoleOutput? _console;
		private INuGetService? _nuGetService;

		/// <summary>
		/// Gets or sets the specification file(s) with optional kind.
		/// Format: path[:kind], where kind defaults to openapi.
		/// </summary>
		[CliOption(Description = "Specification file(s) with optional kind (format: path[:kind], kind defaults to openapi). Can be specified multiple times.", Required = true)]
		public string[] Spec { get; set; } = [];

		/// <summary>
		/// Gets or sets the package ID for the generated NuGet package.
		/// </summary>
		[CliOption(Description = "Package ID for the generated NuGet package", Required = true)]
		public required string PackageId { get; set; }

		/// <summary>
		/// Gets or sets the package version.
		/// </summary>
		[CliOption(Description = "Package version", Required = true)]
		public required string Version { get; set; }

		/// <summary>
		/// Gets or sets the package authors.
		/// </summary>
		[CliOption(Description = "Package authors", Required = false)]
		public string Authors { get; set; } = "ConcordIO";

		/// <summary>
		/// Gets or sets the package description.
		/// </summary>
		[CliOption(Description = "Package description", Required = false)]
		public string? Description { get; set; }

		/// <summary>
		/// Gets or sets the output directory for generated files and packages.
		/// </summary>
		[CliOption(Description = "Output directory for generated files and packages", Required = false)]
		public string Output { get; set; } = ".";

		/// <summary>
		/// Gets or sets whether to also generate and pack a client package.
		/// </summary>
		[CliOption(Description = "Also generate and pack client package", Required = false)]
		public bool Client { get; set; } = true;

		/// <summary>
		/// Gets or sets the client package ID. Defaults to PackageId.Client.
		/// </summary>
		[CliOption(Description = "Client package ID (defaults to PackageId.Client)", Required = false)]
		public string? ClientPackageId { get; set; }

		/// <summary>
		/// Gets or sets the client class name for OpenAPI client generation.
		/// </summary>
		[CliOption(Description = "Client class name (for OpenAPI client generation)", Required = false)]
		public string? ClientClassName { get; set; }

		/// <summary>
		/// Gets or sets additional NSwag options in key=value format (OpenAPI only).
		/// </summary>
		[CliOption(Description = "Additional NSwag options in key=value format (OpenAPI only)", Required = false)]
		public string[]? NswagOptions { get; set; }

		/// <summary>
		/// Gets or sets additional client options in key=value format (AsyncAPI only).
		/// </summary>
		[CliOption(Description = "Additional client options in key=value format (AsyncAPI only)", Required = false)]
		public string[]? ClientOptions { get; set; }

		/// <summary>
		/// Gets or sets additional package properties in key=value format.
		/// </summary>
		[CliOption(Description = "Additional package properties in key=value format", Required = false)]
		public string[]? PackageProperties { get; set; }

		/// <summary>
		/// Gets the template renderer. Used for dependency injection in tests.
		/// </summary>
		internal ITemplateRenderer TemplateRenderer => _templateRenderer ??= new TemplateRenderer();

		/// <summary>
		/// Gets the file system. Used for dependency injection in tests.
		/// </summary>
		internal IFileSystem FileSystem => _fileSystem ??= new FileSystem();

		/// <summary>
		/// Gets the console output service. Used for dependency injection in tests.
		/// </summary>
		internal IConsoleOutput Console => _console ??= new ConsoleOutput();

		/// <summary>
		/// Gets the NuGet service. Used for dependency injection in tests.
		/// </summary>
		internal INuGetService NuGetService => _nuGetService ??= new NuGetService();

		private static readonly IReadOnlyList<string> ValidKinds = SpecKind.All;

		/// <summary>
		/// Executes the pack command.
		/// </summary>
		/// <returns>Exit code: 0 for success, non-zero for failure.</returns>
		public async Task<int> RunAsync()
		{
			// Parse spec entries (with full paths for copying)
			var specs = SpecHelpers.ParseSpecEntries(Spec)
				.Select(e => (e.FilePath, e.FileName, e.Kind))
				.ToList();
			if (specs.Count == 0)
			{
				Console.WriteError("Error: At least one specification file is required.");
				return 1;
			}

			// Validate all spec files exist
			foreach (var (filePath, _, _) in specs)
			{
				if (!FileSystem.FileExists(filePath))
				{
					Console.WriteError($"Error: Specification file not found: {filePath}");
					return 1;
				}
			}

			// Validate all kinds
			var invalidKinds = specs.Select(s => s.Kind).Distinct().Except(ValidKinds).ToList();
			if (invalidKinds.Count > 0)
			{
				Console.WriteError($"Error: Invalid kind(s): {string.Join(", ", invalidKinds)}. Must be 'openapi', 'proto', or 'asyncapi'.");
				return 1;
			}

			// Group specs by kind
			var specsByKind = specs
				.GroupBy(s => s.Kind)
				.ToDictionary(g => g.Key, g => g.ToList());

			var kindsSummary = string.Join(", ", specsByKind.Select(kvp => $"{kvp.Value.Count} {kvp.Key}"));
			var description = Description ?? $"Contract specifications for {PackageId} ({kindsSummary})";

			// Create output directory
			var outputDir = Path.GetFullPath(Output);
			FileSystem.CreateDirectory(outputDir);

			// Copy spec files to output directory organized by kind
			CopySpecFiles(specs, outputDir);

			// Build specs dictionary with just file names for template rendering
			var specFilesByKind = specsByKind.ToDictionary(
				kvp => kvp.Key,
				kvp => kvp.Value.Select(s => s.FileName).ToList());

			// Generate and pack contract package
			var contractResult = await GenerateAndPackContractAsync(outputDir, specFilesByKind, description);
			if (!contractResult.Success)
			{
				Console.WriteError($"Error: Failed to pack contract package. {contractResult.Output}");
				return contractResult.ExitCode;
			}

			Console.WriteLine($"Created: {contractResult.NupkgPath ?? Path.Combine(outputDir, "*.nupkg")}");

			// Generate and pack client package if requested
			if (Client)
			{
				var clientResult = await GenerateAndPackClientAsync(outputDir, specFilesByKind);
				if (!clientResult.Success)
				{
					Console.WriteError($"Error: Failed to pack client package. {clientResult.Output}");
					return clientResult.ExitCode;
				}

				Console.WriteLine($"Created: {clientResult.NupkgPath ?? Path.Combine(outputDir, "*.Client.nupkg")}");
			}

			Console.WriteLine($"Successfully created package(s) in: {outputDir}");
			return 0;
		}

		private void CopySpecFiles(List<(string FilePath, string FileName, string Kind)> specs, string outputDir)
		{
			// Check for duplicate filenames across all specs
			var fileNames = specs.Select(s => s.FileName).ToList();
			var duplicates = fileNames.GroupBy(f => f).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

			if (duplicates.Count > 0)
			{
				throw new InvalidOperationException(
					$"Error: Duplicate spec file names detected: {string.Join(", ", duplicates)}. " +
					"Each spec file must have a unique file name to avoid overwriting in the package.");
			}

			foreach (var (filePath, fileName, kind) in specs)
			{
				// Copy to kind-specific folder (e.g., openapi/, asyncapi/)
				var kindDir = Path.Combine(outputDir, kind);
				FileSystem.CreateDirectory(kindDir);
				var destPath = Path.Combine(kindDir, fileName);
				FileSystem.CopyFile(filePath, destPath, overwrite: true);

				// Also copy to root for nuspec file references (contentFiles)
				var rootDestPath = Path.Combine(outputDir, fileName);
				FileSystem.CopyFile(filePath, rootDestPath, overwrite: true);
			}
		}

		private async Task<NuGetPackResult> GenerateAndPackContractAsync(
			string outputDir,
			Dictionary<string, List<string>> specsByKind,
			string description)
		{
			var generator = new ContractPackageGenerator(TemplateRenderer, FileSystem);
			var options = new ContractPackageOptions
			{
				PackageId = PackageId,
				Version = Version,
				Authors = Authors,
				Description = description,
				OutputDirectory = outputDir,
				PackageProperties = StringHelpers.ParseKeyValuePairs(PackageProperties),
				SpecsByKind = specsByKind
			};

			var generated = await generator.GenerateContractPackageAsync(options);
			Console.WriteLine($"Generated: {generated.NuspecPath}");
			Console.WriteLine($"Generated: {generated.TargetsPath}");

			// Pack the nuspec
			return await NuGetService.PackAsync(generated.NuspecPath, outputDir, outputDir);
		}

		private async Task<NuGetPackResult> GenerateAndPackClientAsync(
			string outputDir,
			Dictionary<string, List<string>> specsByKind)
		{
			var clientPackageId = ClientPackageId ?? $"{PackageId}.Client";
			var hasOpenApi = specsByKind.ContainsKey(SpecKind.OpenApi);
			var hasAsyncApi = specsByKind.ContainsKey(SpecKind.AsyncApi);

			var clientClass = ClientClassName ?? $"{StringHelpers.SanitizeClassName(PackageId)}Client";
			var normalizedNswagOptions = GetNormalizedNswagOptions(hasOpenApi);
			var clientOptions = hasAsyncApi
				? StringHelpers.ParseKeyValuePairs(ClientOptions)
					.Select(kvp => new KeyValuePair<string, string>(StringHelpers.NormalizePrefix("ConcordIOClient", kvp.Key), kvp.Value))
					.ToList()
				: [];

			var generator = new ContractPackageGenerator(TemplateRenderer, FileSystem);
			var options = new ClientPackageOptions
			{
				ClientPackageId = clientPackageId,
				ContractPackageId = PackageId,
				ContractVersion = Version,
				Version = Version,
				Authors = Authors,
				Description = $"Client generator for {PackageId}. Generates code from contract specifications.",
				OutputDirectory = outputDir,
				NSwagClientClassName = clientClass,
				NSwagOutputPath = clientClass,
				NSwagOptions = normalizedNswagOptions,
				ClientOptions = clientOptions,
				PackageProperties = StringHelpers.ParseKeyValuePairs(PackageProperties),
				SpecsByKind = specsByKind
			};

			var generated = await generator.GenerateClientPackageAsync(options);
			Console.WriteLine($"Generated: {generated.NuspecPath}");
			Console.WriteLine($"Generated: {generated.TargetsPath}");

			// Pack the nuspec
			return await NuGetService.PackAsync(generated.NuspecPath, outputDir, outputDir);
		}

		private List<KeyValuePair<string, string>> GetNormalizedNswagOptions(bool hasOpenApi)
		{
			if (!hasOpenApi)
			{
				return [];
			}

			var parsedNswagOptions = StringHelpers.ParseKeyValuePairs(NswagOptions);
			var normalizedNswagOptions = parsedNswagOptions
				.Select(kvp => new KeyValuePair<string, string>(StringHelpers.NormalizePrefix("NSwag", kvp.Key), kvp.Value))
				.ToList();

			var stjOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				{ "NSwagJsonLibrary", "SystemTextJson" },
				{ "NSwagJsonPolymorphicSerializationStyle", "SystemTextJson" },
				{ "NSwagGenerateNullableReferenceTypes", "false" }
			};

			if (!normalizedNswagOptions.Any(o => stjOptions.ContainsKey(o.Key)))
			{
				normalizedNswagOptions.AddRange(stjOptions);
			}

			if (!normalizedNswagOptions.Any(o => string.Equals("NSwagGenerateExceptionClasses", o.Key, StringComparison.OrdinalIgnoreCase)))
			{
				normalizedNswagOptions.Add(new("NSwagGenerateExceptionClasses", "true"));
			}

			return normalizedNswagOptions;
		}
	}
}
