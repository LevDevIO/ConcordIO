using System.Diagnostics;

namespace ConcordIO.Tool.Services;

/// <summary>
/// Default implementation of <see cref="INuGetService"/> that uses the nuget CLI.
/// </summary>
public class NuGetService : INuGetService
{
    public async Task<int> DownloadPackageAsync(string outputDir, string packageId, string? version, bool prerelease)
    {
        var arguments = $"install {packageId} -OutputDirectory {outputDir}" 
            + (version != null ? $" -Version {version}" : "") 
            + (prerelease ? " -Prerelease" : "");

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

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        Console.WriteLine(output);
        Console.Error.WriteLine(error);

        return process.ExitCode;
    }
}
