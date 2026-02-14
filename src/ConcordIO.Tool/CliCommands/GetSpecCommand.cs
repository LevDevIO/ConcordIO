namespace ConcordIO.Tool.CliCommands;

using System;

using ConcordIO.Tool.Services;

using DotMake.CommandLine;

public partial class RootCommand
{
	[CliCommand(Name = "get-spec", Description = "Retrieve the OpenAPI/Protobuf/AsyncAPI specification from a NuGet package")]
	public class GetSpecCommand
	{
		private INuGetService? _nuGetService;
		private IConsoleOutput? _console;

		[CliOption(Description = "Package ID of the NuGet package to retrieve the specification from", Required = true)]
		public required string PackageId
		{
			get; set;
		}

		[CliOption(Description = "Version of the NuGet package, defaults to latest", Required = false)]
		public string? Version
		{
			get; set;
		}

		[CliOption(Description = "Whether to include prerelease versions when retrieving the package", Required = false)]
		public bool Prerelease { get; set; } = false;

		[CliOption(Description = "Output path for the retrieved specification file, defaults to copying original file to the current folder", Required = false)]
		public string? OutputPath
		{
			get; set;
		}

		[CliOption(Description = "Whether to overwrite the output file if it already exists", Required = false)]
		public bool OverwriteOutput { get; set; } = true;

		[CliOption(Description = "Contract kind: openapi, proto, or asyncapi", Required = false)]
		public string Kind { get; set; } = SpecKind.OpenApi;

		[CliOption(Description = "Working directory for downloading the package, defaults to a temp directory", Required = false)]
		public string? WorkingDirectory
		{
			get; set;
		}

		/// <summary>
		/// Gets the NuGet service. Used for dependency injection in tests.
		/// </summary>
		internal INuGetService NuGetService => _nuGetService ??= new NuGetService();

		/// <summary>
		/// Gets the console output service. Used for dependency injection in tests.
		/// </summary>
		internal IConsoleOutput ConsoleOutput => _console ??= new ConsoleOutput();

		public GetSpecCommand()
		{
		}

		/// <summary>
		/// Constructor for dependency injection (testing).
		/// </summary>
		public GetSpecCommand(INuGetService nuGetService, IConsoleOutput? console = null)
		{
			_nuGetService = nuGetService;
			_console = console;
		}

		public async Task<int> RunAsync()
		{
			// Validate kind
			if (!SpecKind.All.Contains(Kind))
			{
				ConsoleOutput.WriteError($"Error: Invalid kind '{Kind}'. Supported kinds are: {string.Join(", ", SpecKind.All)}");
				return 1;
			}

			using var tempDir = new TempDirectoryScope(WorkingDirectory, ConsoleOutput);
			var workingDirectory = tempDir.Path;

			ConsoleOutput.WriteLine($"Downloading NuGet package '{PackageId}' to '{workingDirectory}'...");
			await NuGetService.DownloadPackageAsync(workingDirectory, PackageId, Version, prerelease: Prerelease);

			// Find the package directory
			var packageDirs = Directory.EnumerateDirectories(workingDirectory).ToList();
			if (packageDirs.Count == 0)
			{
				ConsoleOutput.WriteError($"Error: No package directory found in '{workingDirectory}' after downloading '{PackageId}'.");
				return 1;
			}
			if (packageDirs.Count > 1)
			{
				ConsoleOutput.WriteError($"Error: Multiple package directories found in '{workingDirectory}'. Expected exactly one.");
				return 1;
			}
			var packageDir = packageDirs[0];

			// Find the spec directory based on kind
			var specDir = Path.Combine(packageDir, Kind);
			if (!Directory.Exists(specDir))
			{
				ConsoleOutput.WriteError($"Error: No '{Kind}' directory found in the NuGet package '{PackageId}'.");
				return 1;
			}

			// Find the spec file(s) in the directory
			var specFiles = Directory.EnumerateFiles(specDir)
				.Where(f => f.EndsWith(".yaml") || f.EndsWith(".yml") || f.EndsWith(".json") || f.EndsWith(".proto"))
				.ToList();

			if (specFiles.Count == 0)
			{
				ConsoleOutput.WriteError($"Error: No specification files found in '{specDir}'. Expected .yaml, .yml, .json, or .proto files.");
				return 1;
			}
			if (specFiles.Count > 1)
			{
				ConsoleOutput.WriteError($"Error: Multiple specification files found in '{specDir}'. Expected exactly one. Found: {string.Join(", ", specFiles.Select(Path.GetFileName))}");
				return 1;
			}

			var file = specFiles[0];
			var outputPath = OutputPath ?? Path.Combine(Environment.CurrentDirectory, Path.GetFileName(file));
			ConsoleOutput.WriteLine($"Copying specification file '{file}' to '{outputPath}'...");
			File.Copy(file, outputPath, overwrite: OverwriteOutput);
			return 0;
		}
	}
}
