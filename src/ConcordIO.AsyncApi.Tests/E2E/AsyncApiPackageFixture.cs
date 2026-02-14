using System.Diagnostics;

namespace ConcordIO.AsyncApi.Tests.E2E;

/// <summary>
/// Fixture that builds and packs the client and server projects once for all E2E tests.
/// </summary>
public class AsyncApiPackageFixture : IAsyncLifetime
{
	public string TestDir { get; private set; } = null!;
	public string PackagesDir { get; private set; } = null!;
	public string NugetCacheDir { get; private set; } = null!;
	public string ClientProjectPath { get; private set; } = null!;
	public string ServerProjectPath { get; private set; } = null!;

	public async Task InitializeAsync()
	{
		TestDir = Path.Combine(Path.GetTempPath(), "ConcordIO.AsyncApi.Tests", Path.GetRandomFileName().Replace(".", ""));
		PackagesDir = Path.Combine(TestDir, "packages");
		NugetCacheDir = Path.Combine(TestDir, "nuget-cache");
		Directory.CreateDirectory(TestDir);
		Directory.CreateDirectory(PackagesDir);
		Directory.CreateDirectory(NugetCacheDir);

		var testAssemblyDir = Path.GetDirectoryName(typeof(AsyncApiPackageFixture).Assembly.Location)!;
		ClientProjectPath = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", "..", "ConcordIO.AsyncApi.Client", "ConcordIO.AsyncApi.Client.csproj"));
		ServerProjectPath = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", "..", "ConcordIO.AsyncApi.Server", "ConcordIO.AsyncApi.Server.csproj"));

		var clientProjectDir = Path.GetDirectoryName(ClientProjectPath)!;
		var serverProjectDir = Path.GetDirectoryName(ServerProjectPath)!;

		var (clientBuildExitCode, clientBuildOutput) = await RunDotNetAsync("build", clientProjectDir, "-c Release");
		if (clientBuildExitCode != 0)
			throw new Exception($"Client project build failed: {clientBuildOutput}");

		var (serverBuildExitCode, serverBuildOutput) = await RunDotNetAsync("build", serverProjectDir, "-c Release");
		if (serverBuildExitCode != 0)
			throw new Exception($"Server project build failed: {serverBuildOutput}");

		var (clientPackExitCode, clientPackOutput) = await RunDotNetAsync("pack", clientProjectDir,
			$"-c Release -o \"{PackagesDir}\"");
		if (clientPackExitCode != 0)
			throw new Exception($"Client project pack failed: {clientPackOutput}");

		var (serverPackExitCode, serverPackOutput) = await RunDotNetAsync("pack", serverProjectDir,
			$"-c Release -o \"{PackagesDir}\"");
		if (serverPackExitCode != 0)
			throw new Exception($"Server project pack failed: {serverPackOutput}");
	}

	public Task DisposeAsync()
	{
		try
		{
			Directory.Delete(TestDir, recursive: true);
		}
		catch
		{
			// Ignore cleanup errors
		}
		return Task.CompletedTask;
	}

	private async Task<(int ExitCode, string Output)> RunDotNetAsync(string command, string workingDir, string args = "")
	{
		using var process = new Process();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = "dotnet",
			Arguments = $"{command} {AsyncApiE2ECommandVerbosity.AddDotNetVerbosity(args)}",
			WorkingDirectory = workingDir,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		process.StartInfo.Environment["NUGET_PACKAGES"] = NugetCacheDir;

		process.Start();

		// Start reading streams concurrently to avoid pipe buffer deadlock
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
			throw new TimeoutException($"dotnet {command} timed out after 3 minutes in {workingDir}");
		}

		var output = await outputTask;
		var error = await errorTask;

		return (process.ExitCode, output + error);
	}
}
