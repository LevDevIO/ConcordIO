using System.Diagnostics;

namespace ConcordIO.Tool.Tests.E2E;

/// <summary>
/// Shared fixture for integration tests that provides common infrastructure
/// for running the ConcordIO tool and creating test packages.
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
	private readonly string _baseTestDir;

	/// <summary>
	/// Path to the ConcordIO.Tool project file.
	/// </summary>
	public string ToolProjectPath
	{
		get;
	}

	/// <summary>
	/// Path to the ConcordIO.AsyncApi.Client project file.
	/// </summary>
	public string AsyncApiClientProjectPath
	{
		get;
	}

	public IntegrationTestFixture()
	{
		_baseTestDir = Path.Combine(Path.GetTempPath(), "ConcordIO.IntegrationTests");
		Directory.CreateDirectory(_baseTestDir);

		// Find project paths relative to the test assembly
		var testAssemblyDir = Path.GetDirectoryName(typeof(IntegrationTestFixture).Assembly.Location)!;
		ToolProjectPath = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", "..", "ConcordIO.Tool", "ConcordIO.Tool.csproj"));
		AsyncApiClientProjectPath = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", "..", "ConcordIO.AsyncApi.Client", "ConcordIO.AsyncApi.Client.csproj"));
	}

	public async Task InitializeAsync()
	{
		// Pre-build the tool project to avoid build time in each test
		var (exitCode, output) = await RunDotNetAsync("build", Path.GetDirectoryName(ToolProjectPath)!, "-c Debug");
		if (exitCode != 0)
		{
			throw new Exception($"Failed to pre-build ConcordIO.Tool: {output}");
		}
	}

	public Task DisposeAsync()
	{
		// Cleanup is handled by individual test contexts
		return Task.CompletedTask;
	}

	/// <summary>
	/// Creates a new isolated test context for a single test.
	/// Each test gets its own directory to avoid conflicts.
	/// </summary>
	public TestContext CreateTestContext(string testName)
	{
		return new TestContext(this, _baseTestDir, testName);
	}

	internal async Task<(int ExitCode, string Output)> RunDotNetAsync(string command, string workingDir, string args = "")
	{
		using var process = new Process();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = "dotnet",
			Arguments = $"{command} {CommandVerbosity.AddDotNetVerbosity(command, args)}",
			WorkingDirectory = workingDir,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

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

/// <summary>
/// Isolated test context for a single test run.
/// Provides its own directories and cleanup.
/// </summary>
public class TestContext : IDisposable
{
	private readonly IntegrationTestFixture _fixture;

	public string TestDir
	{
		get;
	}
	public string PackagesDir
	{
		get;
	}
	public string NuGetCacheDir
	{
		get;
	}
	public string ToolProjectPath => _fixture.ToolProjectPath;
	public string AsyncApiClientProjectPath => _fixture.AsyncApiClientProjectPath;

	internal TestContext(IntegrationTestFixture fixture, string baseTestDir, string testName)
	{
		_fixture = fixture;
		var uniqueId = Path.GetRandomFileName().Replace(".", "");
		TestDir = Path.Combine(baseTestDir, $"{testName}_{uniqueId}");
		PackagesDir = Path.Combine(TestDir, "packages");
		NuGetCacheDir = Path.Combine(TestDir, "nuget-cache");

		Directory.CreateDirectory(TestDir);
		Directory.CreateDirectory(PackagesDir);
		Directory.CreateDirectory(NuGetCacheDir);
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(TestDir, recursive: true);
		}
		catch
		{
			// Ignore cleanup errors
		}
	}

	/// <summary>
	/// Runs the ConcordIO tool with the specified arguments.
	/// </summary>
	public async Task<(int ExitCode, string Output)> RunToolAsync(string args)
	{
		// Use net10.0 framework since ConcordIO.Tool targets multiple frameworks (net8.0, net9.0, net10.0).
		// Use --no-build because the fixture pre-builds once in InitializeAsync; this prevents concurrent
		// file-lock/contention issues when multiple test processes execute dotnet commands.
		return await RunDotNetAsync("run", Path.GetDirectoryName(ToolProjectPath)!, $"--framework net10.0 --no-build -- {args}");
	}

	/// <summary>
	/// Runs a dotnet command in the specified directory.
	/// </summary>
	public async Task<(int ExitCode, string Output)> RunDotNetAsync(string command, string workingDir, string args = "")
	{
		using var process = new Process();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = "dotnet",
			Arguments = $"{command} {CommandVerbosity.AddDotNetVerbosity(command, args)}",
			WorkingDirectory = workingDir,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		process.StartInfo.Environment["NUGET_PACKAGES"] = NuGetCacheDir;

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

	/// <summary>
	/// Runs a process with the specified arguments.
	/// </summary>
	public async Task<(int ExitCode, string Output)> RunProcessAsync(string fileName, string arguments, string workingDir)
	{
		using var process = new Process();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = fileName,
			Arguments = CommandVerbosity.AddNuGetVerbosityIfNeeded(fileName, arguments),
			WorkingDirectory = workingDir,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		process.StartInfo.Environment["NUGET_PACKAGES"] = NuGetCacheDir;

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
			throw new TimeoutException($"{fileName} timed out after 3 minutes in {workingDir}");
		}

		var output = await outputTask;
		var error = await errorTask;

		return (process.ExitCode, output + error);
	}

	/// <summary>
	/// Creates a NuGet package from a nuspec file.
	/// </summary>
	public async Task CreateNuGetPackageAsync(string packageDir, string packageId)
	{
		var nuspecPath = Directory.GetFiles(packageDir, "*.nuspec")
			.First(f => Path.GetFileName(f).StartsWith(packageId, StringComparison.OrdinalIgnoreCase));
		var (exitCode, output) = await RunProcessAsync(
			"nuget",
			$"pack \"{nuspecPath}\" -OutputDirectory \"{PackagesDir}\"",
			packageDir);

		if (exitCode != 0)
		{
			throw new Exception($"Failed to create NuGet package: {output}");
		}
	}

	/// <summary>
	/// Creates a nuget.config file pointing to the local packages directory.
	/// </summary>
	public async Task CreateNuGetConfigAsync(string projectDir)
	{
		var nugetConfig = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="{PackagesDir}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """;
		await File.WriteAllTextAsync(Path.Combine(projectDir, "nuget.config"), nugetConfig);
	}
}

/// <summary>
/// Collection definition for integration tests that share the fixture.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
	public const string Name = "Integration Tests";
}

internal static class CommandVerbosity
{
	// Use 'normal' verbosity in CI to reduce output volume and prevent pipe buffer deadlocks.
	// Local debugging can override via environment variable if needed.
	private const string DotNetVerbosity = "-v normal";

	public static string AddDotNetVerbosity(string command, string args)
	{
		if (string.Equals(command, "add", StringComparison.OrdinalIgnoreCase))
		{
			return args;
		}

		if (string.IsNullOrWhiteSpace(args))
		{
			return DotNetVerbosity;
		}

		if (args.TrimStart().StartsWith("--", StringComparison.Ordinal))
		{
			return $"{DotNetVerbosity} {args}";
		}

		var separatorIndex = args.IndexOf(" -- ", StringComparison.Ordinal);
		var commandArgs = separatorIndex < 0 ? args : args[..separatorIndex];
		var passThroughArgs = separatorIndex < 0 ? string.Empty : args[separatorIndex..];

		var normalizedArgs = NormalizeDotNetVerbosityArgs(commandArgs);
		if (string.IsNullOrWhiteSpace(normalizedArgs))
		{
			return string.IsNullOrWhiteSpace(passThroughArgs)
				? DotNetVerbosity
				: $"{DotNetVerbosity}{passThroughArgs}";
		}

		return string.IsNullOrWhiteSpace(passThroughArgs)
			? $"{normalizedArgs} {DotNetVerbosity}"
			: $"{normalizedArgs} {DotNetVerbosity}{passThroughArgs}";
	}

	private static string NormalizeDotNetVerbosityArgs(string args)
	{
		if (string.IsNullOrWhiteSpace(args))
		{
			return string.Empty;
		}

		var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var filtered = new List<string>(parts.Length);

		for (var i = 0; i < parts.Length; i++)
		{
			var part = parts[i];
			if (string.Equals(part, "-v", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(part, "--verbosity", StringComparison.OrdinalIgnoreCase))
			{
				i++;
				continue;
			}

			if (part.StartsWith("--verbosity=", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			filtered.Add(part);
		}

		return string.Join(' ', filtered);
	}

	public static string AddNuGetVerbosityIfNeeded(string fileName, string args)
	{
		if (!string.Equals(fileName, "nuget", StringComparison.OrdinalIgnoreCase))
		{
			return args;
		}

		return args.Contains("-Verbosity", StringComparison.OrdinalIgnoreCase)
			? args
			: $"{args} -Verbosity detailed";
	}
}
