using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ConcordIO.Tool.Services;

/// <summary>
/// Default implementation of <see cref="INuGetService"/> that uses the nuget CLI.
/// </summary>
public partial class NuGetService : INuGetService
{
	public async Task<int> DownloadPackageAsync(string outputDir, string packageId, string? version, bool prerelease)
	{
		var arguments = $"install \"{packageId}\" -OutputDirectory \"{outputDir}\""
			+ (version != null ? $" -Version \"{version}\"" : "")
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

	/// <inheritdoc />
	public async Task<NuGetPackResult> PackAsync(string nuspecPath, string outputDir, string basePath)
	{
		var arguments = $"pack \"{nuspecPath}\" -OutputDirectory \"{outputDir}\" -BasePath \"{basePath}\"";

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
		var combinedOutput = output + error;

		// Parse the output to find the created .nupkg path
		// NuGet outputs something like: "Successfully created package 'C:\path\to\Package.1.0.0.nupkg'."
		string? nupkgPath = null;
		if (process.ExitCode == 0)
		{
			var match = NupkgPathRegex().Match(combinedOutput);
			if (match.Success)
			{
				nupkgPath = match.Groups[1].Value;
			}
		}

		return new NuGetPackResult
		{
			ExitCode = process.ExitCode,
			Output = combinedOutput,
			NupkgPath = nupkgPath
		};
	}

	[GeneratedRegex(@"Successfully created package '([^']+\.nupkg)'", RegexOptions.IgnoreCase)]
	private static partial Regex NupkgPathRegex();
}
