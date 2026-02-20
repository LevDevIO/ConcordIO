using System.Diagnostics;

namespace ConcordIO.AsyncApi.Tests.E2E;

/// <summary>
/// Fixture that builds and packs the client and server projects once for all E2E tests.
/// </summary>
public class AsyncApiPackageFixture : IAsyncLifetime
{
	/// <summary>Gets the root temporary directory used for AsyncAPI E2E test artifacts.</summary>
	/// <remarks>This folder is created per test run and cleaned up on fixture disposal.</remarks>
	/// <value>The absolute path to the test root directory.</value>
	/// <example><code>var root = fixture.TestDir;</code></example>
	public string TestDir { get; private set; } = null!;
	/// <summary>Gets the directory where packed AsyncAPI test packages are written.</summary>
	/// <remarks>Packages are produced via <c>dotnet pack</c> into this location.</remarks>
	/// <value>The absolute path to the local package feed directory.</value>
	/// <example><code>var packages = fixture.PackagesDir;</code></example>
	public string PackagesDir { get; private set; } = null!;
	/// <summary>Gets the NuGet global-packages folder used during E2E restores.</summary>
	/// <remarks>This isolates test restores from the user-wide cache.</remarks>
	/// <value>The absolute path to the test-local NuGet cache directory.</value>
	/// <example><code>var cache = fixture.NugetCacheDir;</code></example>
	public string NugetCacheDir { get; private set; } = null!;
	/// <summary>Gets the absolute path to the AsyncAPI client project file.</summary>
	/// <remarks>This path is resolved from the test assembly location.</remarks>
	/// <value>The fully-qualified path to ConcordIO.AsyncApi.Client.csproj.</value>
	/// <example><code>var clientCsproj = fixture.ClientProjectPath;</code></example>
	public string ClientProjectPath { get; private set; } = null!;
	/// <summary>Gets the absolute path to the AsyncAPI server project file.</summary>
	/// <remarks>This path is resolved from the test assembly location.</remarks>
	/// <value>The fully-qualified path to ConcordIO.AsyncApi.Server.csproj.</value>
	/// <example><code>var serverCsproj = fixture.ServerProjectPath;</code></example>
	public string ServerProjectPath { get; private set; } = null!;
	/// <summary>Gets the packed AsyncAPI client package version used by E2E test projects.</summary>
	/// <remarks>This value is parsed from the generated <c>.nupkg</c> file name.</remarks>
	/// <value>The client package version string (can include pre-release labels).</value>
	/// <example><code>var version = fixture.ClientPackageVersion;</code></example>
	public string ClientPackageVersion { get; private set; } = null!;
	/// <summary>Gets the packed AsyncAPI server package version used by E2E test projects.</summary>
	/// <remarks>This value is parsed from the generated <c>.nupkg</c> file name.</remarks>
	/// <value>The server package version string (can include pre-release labels).</value>
	/// <example><code>var version = fixture.ServerPackageVersion;</code></example>
	public string ServerPackageVersion { get; private set; } = null!;

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

		var (clientBuildExitCode, clientBuildOutput) = await RunDotNetAsync("build", clientProjectDir, "-c Release --no-restore");
		if (clientBuildExitCode != 0)
			throw new Exception($"Client project build failed: {clientBuildOutput}");

		var (serverBuildExitCode, serverBuildOutput) = await RunDotNetAsync("build", serverProjectDir, "-c Release --no-restore");
		if (serverBuildExitCode != 0)
			throw new Exception($"Server project build failed: {serverBuildOutput}");

		var (clientPackExitCode, clientPackOutput) = await RunDotNetAsync("pack", clientProjectDir,
			$"-c Release --no-restore -o \"{PackagesDir}\"");
		if (clientPackExitCode != 0)
			throw new Exception($"Client project pack failed: {clientPackOutput}");

		var (serverPackExitCode, serverPackOutput) = await RunDotNetAsync("pack", serverProjectDir,
			$"-c Release --no-restore -o \"{PackagesDir}\"");
		if (serverPackExitCode != 0)
			throw new Exception($"Server project pack failed: {serverPackOutput}");

		ClientPackageVersion = GetPackedPackageVersion("ConcordIO.AsyncApi.Client");
		ServerPackageVersion = GetPackedPackageVersion("ConcordIO.AsyncApi.Server");
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
			RedirectStandardInput = false,
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
			// Ensure all streams are fully consumed before continuing
			await Task.WhenAll(outputTask, errorTask);
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

	private string GetPackedPackageVersion(string packageId)
	{
		var packagePath = Directory.GetFiles(PackagesDir, $"{packageId}.*.nupkg").FirstOrDefault();
		if (packagePath == null)
			throw new FileNotFoundException($"Unable to locate packed {packageId} package in {PackagesDir}.");

		var fileName = Path.GetFileNameWithoutExtension(packagePath);
		var prefix = packageId + ".";
		if (fileName == null || !fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException($"Packed {packageId} filename '{fileName}' does not match expected pattern '{prefix}<version>.nupkg'.");

		return fileName.Substring(prefix.Length);
	}
}
