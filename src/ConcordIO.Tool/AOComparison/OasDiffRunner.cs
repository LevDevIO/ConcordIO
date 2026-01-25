using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ConcordIO.Tool.AOComparison;

/// <summary>
/// Wrapper for running oasdiff commands to compare OpenAPI specifications.
/// </summary>
public class OasDiffRunner
{
    private readonly string _oasdiffPath;

    public OasDiffRunner()
    {
        _oasdiffPath = GetOasDiffPath();
    }

    /// <summary>
    /// Gets breaking changes between two OpenAPI specs.
    /// </summary>
    public async Task<OasDiffResult> Breaking(string baseSpec, string revisionSpec, string arguments)
    {
        return await Run($"breaking \"{baseSpec}\" \"{revisionSpec}\" {arguments}");
    }

    /// <summary>
    /// Runs an arbitrary oasdiff command.
    /// </summary>
    public async Task<OasDiffResult> Run(string arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = _oasdiffPath,
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

        return new OasDiffResult
        {
            ExitCode = process.ExitCode,
            Output = output,
            Error = error,
            Breaking = process.ExitCode != 0
        };
    }

    private static string GetOasDiffPath()
    {
        var baseDir = AppContext.BaseDirectory;
        string relativePath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            relativePath = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? Path.Combine("oasdiff", "win-arm64", "oasdiff.exe")
                : Path.Combine("oasdiff", "win-x64", "oasdiff.exe");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            relativePath = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? Path.Combine("oasdiff", "linux-arm64", "oasdiff")
                : Path.Combine("oasdiff", "linux-x64", "oasdiff");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Universal binary for all macOS architectures
            relativePath = Path.Combine("oasdiff", "osx", "oasdiff");
        }
        else
        {
            throw new PlatformNotSupportedException(
                $"oasdiff is not bundled for platform: {RuntimeInformation.OSDescription}");
        }

        var fullPath = Path.Combine(baseDir, relativePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"oasdiff binary not found at: {fullPath}");
        }

        return fullPath;
    }
}

/// <summary>
/// Result of an oasdiff command execution.
/// </summary>
public class OasDiffResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public bool Breaking { get; init; }
}