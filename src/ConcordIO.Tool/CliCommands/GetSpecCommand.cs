namespace ConcordIO.Tool.CliCommands;

using DotMake.CommandLine;
using System;
using System.Diagnostics;

public partial class RootCommand
{
    [CliCommand(Name = "get-spec", Description = "Retrieve the OpenAPI/Protobuf specification from a NuGet package")]
    public class GetSpecCommand
    {
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

        public async Task<int> RunAsync()
        {
            var createTempDir = WorkingDirectory == null;
            string workingDirectory = WorkingDirectory ?? Path.Combine(Path.GetTempPath(), "ConcordIO", Path.GetRandomFileName().Replace(".", ""));

            if (createTempDir)
            {
                Directory.CreateDirectory(workingDirectory);
            }

            Console.WriteLine($"Downloading NuGet package '{PackageId}' to '{workingDirectory}'...");
            await DownloadNuget(workingDirectory, PackageId, Version, prerelease: Prerelease);

            var packageDir = Directory.EnumerateDirectories(workingDirectory).Single();
            var openApiDir = Path.Combine(packageDir, "openapi");

            if (Directory.Exists(openApiDir))
            {
                var file = Directory.EnumerateFiles(openApiDir).Single(f => f.EndsWith(".yaml") || f.EndsWith(".yml") || f.EndsWith(".json"));
                var outputPath = OutputPath ?? Path.Combine(Environment.CurrentDirectory, Path.GetFileName(file));
                Console.WriteLine($"Copying specification file '{file}' to '{outputPath}'...");
                File.Copy(file, outputPath, overwrite: OverwriteOutput);
                return 0;
            }

            throw new NotImplementedException($"No 'openapi' directory found in the NuGet package '{PackageId}', proto not implemented  yet");
        }

        /// <summary>
        /// Runs an arbitrary oasdiff command.
        /// </summary>
        public async Task<int> DownloadNuget(string outputDir, string packageId, string? version, bool prerelease)
        {
            var arguments = $"install {packageId} -OutputDirectory {outputDir}" + (version != null ? $" -Version {version}" : "") + (prerelease ? " -Prerelease" : "");

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "nuget",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            Console.WriteLine(output);
            Console.Error.WriteLine(error);

            return process.ExitCode;
        }
    }
}
