using System.Diagnostics;
using System.Runtime.InteropServices;
using ConcordIO.Tool.Services;

namespace ConcordIO.Tool.AOComparison;

/// <summary>
/// Wrapper for running oasdiff commands to compare OpenAPI specifications.
/// </summary>
public class OasDiffRunner : IOasDiffRunner
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

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return new OasDiffResult
            {
                ExitCode = -1,
                Output = string.Empty,
                Error = $"oasdiff timed out after 3 minutes while running: {arguments}",
                Breaking = false,
                Success = false
            };
        }

        var output = await outputTask;
        var error = await errorTask;

        return new OasDiffResult
        {
            ExitCode = process.ExitCode,
            Output = output,
            Error = error,
            Breaking = process.ExitCode == 1,
            Success = process.ExitCode == 0 || process.ExitCode == 1
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

        // Ensure the binary is executable on Unix platforms.
        // Git on Windows does not preserve the Unix executable bit,
        // so the binary may lack +x after checkout or NuGet install.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(fullPath,
                File.GetUnixFileMode(fullPath) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }

        return fullPath;
    }
}