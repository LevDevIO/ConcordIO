namespace ConcordIO.Tool.CliCommands;

using ConcordIO.Tool.AOComparison;
using DotMake.CommandLine;
using System;
using System.Collections.Generic;
using System.Text;

public partial class RootCommand
{

    [CliCommand(Name = "breaking", Description = "Compare OpenAPI/Protobuf specifications to latest version packed in nuget and report breaking changes")]
    public class BreakingCommand
    {
        [CliOption(Description = "Path to the OpenAPI/Protobuf specification file", Required = true)]
        public required string Spec { get; set; }

        [CliOption(Description = "Package ID for the generated NuGet package", Required = true)]
        public required string PackageId { get; set; }

        [CliOption(Description = "Version of the NuGet package, defaults to latest", Required = false)]
        public string? Version { get; set; }

        [CliOption(Description = "Whether to include prerelease versions when retrieving the package", Required = false)]
        public bool Prerelease { get; set; } = false;

        [CliOption(Description = "Contract kind: openapi or proto", Required = false)]
        public string Kind { get; set; } = "openapi";

        [CliOption(Description = "Working directory for downloading the package, defaults to a temp directory", Required = false)]
        public string? WorkingDirectory { get; set; }

        [CliOption(Description = "Additional command line options for diffing tool in key=value format (can be specified multiple times)", Required = false)]
        public string[]? CliOptions { get; set; }

        public async Task<int> RunAsync()
        {
            var createTempDir = WorkingDirectory == null;
            var workingDirectory = WorkingDirectory ?? Path.Combine(Path.GetTempPath(), "ConcordIO", Path.GetRandomFileName().Replace(".", ""));

            if (createTempDir)
            {
                Directory.CreateDirectory(workingDirectory);
            }

            try
            {
                var nugetSpecPath = Path.Combine(workingDirectory, $"spec_in_nuget{Path.GetExtension(Spec)}");
                var getSpecCommand = new GetSpecCommand
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
                    Console.Error.WriteLine("Error: Failed to retrieve specification from NuGet package.");
                    return getSpecResult;
                }

                var oasdiffRunner = new OasDiffRunner();
                var result = await oasdiffRunner.Breaking(Spec, nugetSpecPath, "-o WARN" + BuildCliOptionsString());

                Console.WriteLine(result.Output);
                Console.Error.WriteLine(result.Error);

                if (result.Breaking)
                {
                    Console.Error.WriteLine("Breaking changes detected.");
                }
                else
                {
                    Console.WriteLine("No breaking changes detected.");
                }

                return result.ExitCode;
            }
            finally
            {
                if (createTempDir)
                {
                    try
                    {
                        Directory.Delete(workingDirectory, true);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Warning: Failed to delete temporary working directory '{workingDirectory}': {ex.Message}");
                    }
                }
            }
        }

        private string BuildCliOptionsString()
        {
            if (CliOptions == null || CliOptions.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var option in CliOptions)
            {
                var parts = option.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length != 2)
                    throw new ArgumentException($"Invalid key=value format: '{option}'");

                var key = parts[0];
                var value = parts[1];
                sb.Append($" --{key} {value}");
            }

            return sb.ToString().TrimStart();
        }
    }
}