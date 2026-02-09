namespace ConcordIO.Tool.CliCommands;

using ConcordIO.Tool.AOComparison;
using ConcordIO.Tool.Services;
using DotMake.CommandLine;
using System;
using System.Collections.Generic;
using System.Text;

public partial class RootCommand
{

    [CliCommand(Name = "breaking", Description = "Compare OpenAPI/Protobuf specifications to latest version packed in nuget and report breaking changes")]
    public class BreakingCommand
    {
        private IConsoleOutput? _console;

        [CliOption(Description = "Path to the OpenAPI/Protobuf specification file", Required = true)]
        public required string Spec { get; set; }

        [CliOption(Description = "Package ID for the generated NuGet package", Required = true)]
        public required string PackageId { get; set; }

        [CliOption(Description = "Version of the NuGet package, defaults to latest", Required = false)]
        public string? Version { get; set; }

        [CliOption(Description = "Whether to include prerelease versions when retrieving the package", Required = false)]
        public bool Prerelease { get; set; } = false;

        [CliOption(Description = "Contract kind: openapi or proto", Required = false)]
        public string Kind { get; set; } = SpecKind.OpenApi;

        [CliOption(Description = "Working directory for downloading the package, defaults to a temp directory", Required = false)]
        public string? WorkingDirectory { get; set; }

        [CliOption(Description = "Additional command line options for diffing tool in key=value format (can be specified multiple times)", Required = false)]
        public string[]? CliOptions { get; set; }

        public async Task<int> RunAsync()
        {
            _console ??= new ConsoleOutput();
            var oasDiffRunner = new OasDiffRunner();

            await using var tempDir = new TempDirectoryScope(WorkingDirectory);
            var workingDirectory = tempDir.Path;

            var nugetSpecPath = Path.Combine(workingDirectory, $"spec_in_nuget{Path.GetExtension(Spec)}");
            
            // Create a new GetSpecCommand instance with required properties
            var getSpecCommand = new GetSpecCommand(new NuGetService(), _console)
            {
                PackageId = PackageId,
                Version = Version,
                Prerelease = Prerelease,
                OutputPath = nugetSpecPath,
                WorkingDirectory = workingDirectory,
                OverwriteOutput = true,
            };

            var getSpecResult = await getSpecCommand.RunAsync();
            if (getSpecResult != 0)
            {
                _console.WriteError("Error: Failed to retrieve specification from NuGet package.");
                return getSpecResult;
            }

            var cliOptionsString = BuildCliOptionsString();
            var result = await oasDiffRunner.Breaking(Spec, nugetSpecPath, "-o WARN" + (string.IsNullOrEmpty(cliOptionsString) ? "" : " " + cliOptionsString));

            _console.WriteLine(result.Output);
            _console.WriteError(result.Error);

            if (result.Breaking)
            {
                _console.WriteError("Breaking changes detected.");
            }
            else
            {
                _console.WriteLine("No breaking changes detected.");
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