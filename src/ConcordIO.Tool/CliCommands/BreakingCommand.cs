namespace ConcordIO.Tool.CliCommands;

using System;
using System.Collections.Generic;
using System.Text;
using ConcordIO.Tool.AOComparison;
using ConcordIO.Tool.Services;
using DotMake.CommandLine;

public partial class RootCommand
{

    [CliCommand(Name = "breaking", Description = "Compare OpenAPI/Protobuf/AsyncAPI specifications to latest version packed in nuget and report breaking changes")]
    public class BreakingCommand
    {
        private IConsoleOutput? _console;
        private INuGetService? _nuGetService;
        private IOasDiffRunner? _oasDiffRunner;

        [CliOption(Description = "Path to the OpenAPI/Protobuf specification file", Required = true)]
        public required string Spec { get; set; }

        [CliOption(Description = "Package ID for the generated NuGet package", Required = true)]
        public required string PackageId { get; set; }

        [CliOption(Description = "Version of the NuGet package, defaults to latest", Required = false)]
        public string? Version { get; set; }

        [CliOption(Description = "Whether to include prerelease versions when retrieving the package", Required = false)]
        public bool Prerelease { get; set; } = false;

        [CliOption(Description = "Contract kind: openapi, proto, or asyncapi", Required = false)]
        public string Kind { get; set; } = SpecKind.OpenApi;

        [CliOption(Description = "Working directory for downloading the package, defaults to a temp directory", Required = false)]
        public string? WorkingDirectory { get; set; }

        [CliOption(Description = "Additional command line options for diffing tool in key=value format (can be specified multiple times)", Required = false)]
        public string[]? CliOptions { get; set; }

        /// <summary>
        /// Gets the console output service. Used for dependency injection in tests.
        /// </summary>
        internal IConsoleOutput Console => _console ??= new ConsoleOutput();

        /// <summary>
        /// Gets the NuGet service. Used for dependency injection in tests.
        /// </summary>
        internal INuGetService NuGetService => _nuGetService ??= new NuGetService();

        /// <summary>
        /// Gets the oasdiff runner. Used for dependency injection in tests.
        /// </summary>
        internal IOasDiffRunner OasDiffRunner => _oasDiffRunner ??= new OasDiffRunner();

        public async Task<int> RunAsync()
        {
            using var tempDir = new TempDirectoryScope(WorkingDirectory, Console);
            var workingDirectory = tempDir.Path;

            var nugetSpecPath = Path.Combine(workingDirectory, $"spec_in_nuget{Path.GetExtension(Spec)}");

            // Create a new GetSpecCommand instance with required properties

            var getSpecCommand = new GetSpecCommand(NuGetService, Console)
            {
                PackageId = PackageId,
                Version = Version,
                Prerelease = Prerelease,
                OutputPath = nugetSpecPath,
                WorkingDirectory = workingDirectory,
                OverwriteOutput = true,
                Kind = Kind,
            };

            var getSpecResult = await getSpecCommand.RunAsync();
            if (getSpecResult != 0)
            {
                Console.WriteError("Error: Failed to retrieve specification from NuGet package.");
                return getSpecResult;
            }

            var cliOptionsString = BuildCliOptionsString();
            var result = await OasDiffRunner.Breaking(Spec, nugetSpecPath, "-o WARN" + (string.IsNullOrEmpty(cliOptionsString) ? "" : " " + cliOptionsString));

            Console.WriteLine(result.Output);
            Console.WriteError(result.Error);

            if (result.Breaking)
            {
                Console.WriteError("Breaking changes detected.");
            }
            else
            {
                Console.WriteLine("No breaking changes detected.");
            }

            return result.ExitCode;
        }

        private string BuildCliOptionsString()
        {
            if (CliOptions == null || CliOptions.Length == 0)
            {
                return string.Empty;
            }

            var parsedPairs = StringHelpers.ParseKeyValuePairs(CliOptions);
            var sb = new StringBuilder();
            foreach (var kvp in parsedPairs)
            {
                sb.Append($" --{kvp.Key} {kvp.Value}");
            }

            return sb.ToString().TrimStart();
        }
    }
}