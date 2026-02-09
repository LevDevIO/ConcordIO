namespace ConcordIO.Tool.CliCommands;

using ConcordIO.Tool.Services;
using DotMake.CommandLine;
using System;

public partial class RootCommand
{
    [CliCommand(Name = "get-spec", Description = "Retrieve the OpenAPI/Protobuf specification from a NuGet package")]
    public class GetSpecCommand
    {
        private INuGetService? _nuGetService;
        private IConsoleOutput? _console;

        [CliOption(Description = "Package ID of the NuGet package to retrieve the specification from", Required = true)]
        public required string PackageId { get; set; }

        [CliOption(Description = "Version of the NuGet package, defaults to latest", Required = false)]
        public string? Version { get; set; }

        [CliOption(Description = "Whether to include prerelease versions when retrieving the package", Required = false)]
        public bool Prerelease { get; set; } = false;

        [CliOption(Description = "Output path for the retrieved specification file, defaults to copying original file to the current folder", Required = false)]
        public string? OutputPath { get; set; }

        [CliOption(Description = "Whether to overwrite the output file if it already exists", Required = false)]
        public bool OverwriteOutput { get; set; } = true;

        [CliOption(Description = "Working directory for downloading the package, defaults to a temp directory", Required = false)]
        public string? WorkingDirectory { get; set; }

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
            await using var tempDir = new TempDirectoryScope(WorkingDirectory, ConsoleOutput);
            var workingDirectory = tempDir.Path;

            ConsoleOutput.WriteLine($"Downloading NuGet package '{PackageId}' to '{workingDirectory}'...");
            await NuGetService.DownloadPackageAsync(workingDirectory, PackageId, Version, prerelease: Prerelease);

            var packageDir = Directory.EnumerateDirectories(workingDirectory).Single();
            var openApiDir = Path.Combine(packageDir, "openapi");

            if (Directory.Exists(openApiDir))
            {
                var file = Directory.EnumerateFiles(openApiDir).Single(f => f.EndsWith(".yaml") || f.EndsWith(".yml") || f.EndsWith(".json"));
                var outputPath = OutputPath ?? Path.Combine(Environment.CurrentDirectory, Path.GetFileName(file));
                ConsoleOutput.WriteLine($"Copying specification file '{file}' to '{outputPath}'...");
                File.Copy(file, outputPath, overwrite: OverwriteOutput);
                return 0;
            }

            throw new NotImplementedException($"No 'openapi' directory found in the NuGet package '{PackageId}', proto not implemented  yet");
        }
    }
}
