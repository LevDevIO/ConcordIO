// Tests for the ConcordIOAssemblyLoadContext fix (PR review)
// Validates that the custom ALC correctly resolves dependencies from the assembly's output directory,
// replacing the previous AppDomain.CurrentDomain.AssemblyResolve approach.

using System.Collections;
using System.Reflection;
using System.Runtime.Loader;

using ConcordIO.AsyncApi.Server;
using ConcordIO.AsyncApi.Server.Tasks;

using Microsoft.Build.Framework;

namespace ConcordIO.AsyncApi.Tests.Server;

public class GenerateAsyncApiTaskTests
{
	#region ConcordIOAssemblyLoadContext: Load() resolves from base directory

	[Fact]
	public void ConcordIOAssemblyLoadContext_Load_ReturnsDll_WhenPresentInBaseDirectory()
	{
		// Arrange - base directory contains ConcordIO.AsyncApi.dll
		var baseDir = Path.GetDirectoryName(typeof(AsyncApiDocumentGenerator).Assembly.Location)!;
		var alc = new ConcordIOAssemblyLoadContext(baseDir);

		try
		{
			// Act - ask the ALC to resolve ConcordIO.AsyncApi by name
			var assemblyName = typeof(AsyncApiDocumentGenerator).Assembly.GetName();
			var resolved = InvokeLoad(alc, assemblyName);

			// Assert - Load() should find the DLL in the base directory
			resolved.Should().NotBeNull(
				because: "Load() must probe basePath for the requested DLL " +
						 "to resolve user-project dependencies at MSBuild time");
		}
		finally
		{
			alc.Unload();
		}
	}

	[Fact]
	public void ConcordIOAssemblyLoadContext_Load_ReturnsNull_WhenDllAbsentFromBaseDirectory()
	{
		// Arrange - an empty temp directory has no assemblies
		var emptyDir = Path.Combine(Path.GetTempPath(), $"alc-test-{Guid.NewGuid()}");
		Directory.CreateDirectory(emptyDir);
		var alc = new ConcordIOAssemblyLoadContext(emptyDir);

		try
		{
			// Act
			var assemblyName = new AssemblyName("NonExistent.Assembly.That.Does.Not.Exist");
			var resolved = InvokeLoad(alc, assemblyName);

			// Assert - Load() returns null so the runtime can fall back to the default context
			resolved.Should().BeNull(
				because: "Load() must return null for unknown assemblies so the runtime " +
						 "can attempt resolution via the parent (default) context");
		}
		finally
		{
			alc.Unload();
			Directory.Delete(emptyDir);
		}
	}

	[Fact]
	public void ConcordIOAssemblyLoadContext_IsCollectible()
	{
		// Arrange
		var baseDir = Path.GetDirectoryName(typeof(AsyncApiDocumentGenerator).Assembly.Location)!;
		var alc = new ConcordIOAssemblyLoadContext(baseDir);

		try
		{
			// Assert - must be collectible so MSBuild can unload it after generation,
			// preventing the memory leak that existed with Assembly.LoadFrom()
			alc.IsCollectible.Should().BeTrue(
				because: "the ALC must be collectible to allow Unload() and prevent " +
						 "memory accumulation in long-running MSBuild/Visual Studio processes");
		}
		finally
		{
			alc.Unload();
		}
	}

	[Fact]
	public void ConcordIOAssemblyLoadContext_LoadedAssembly_CanEnumerateTypes()
	{
		// Arrange - simulates what GenerateAsyncApiTask does: load an assembly with dependencies
		// and enumerate its types for discovery.
		// The base dir contains all required DLLs so the ALC's Load() can resolve them.
		var assemblyPath = typeof(AsyncApiDocumentGenerator).Assembly.Location;
		var baseDir = Path.GetDirectoryName(assemblyPath)!;
		var alc = new ConcordIOAssemblyLoadContext(baseDir);

		try
		{
			// Act - load the assembly via the ALC (as GenerateAsyncApiTask does)
			var assembly = alc.LoadFromAssemblyPath(assemblyPath);

			// Enumerating exported types triggers dependency resolution.
			// Without the Load() override, this would throw FileNotFoundException
			// for assemblies that are in the output dir but not the default context.
			var types = assembly.GetExportedTypes();

			// Assert
			types.Should().NotBeEmpty(
				because: "the assembly should expose exported types after its dependencies " +
						 "are resolved from the base directory via the ALC Load() override");
		}
		finally
		{
			alc.Unload();
		}
	}

	#endregion

	/// <summary>
	/// Invokes the protected <c>Load(AssemblyName)</c> method via reflection,
	/// allowing direct testing without subclassing.
	/// </summary>
	private static Assembly? InvokeLoad(AssemblyLoadContext alc, AssemblyName name)
	{
		var method = alc.GetType()
			.GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Instance)!;
		return (Assembly?)method.Invoke(alc, [name]);
	}
}

/// <summary>
/// Minimal <see cref="IBuildEngine"/> implementation for unit testing MSBuild tasks.
/// </summary>
file sealed class MockBuildEngine : IBuildEngine
{
	public bool ContinueOnError => false;
	public int LineNumberOfTaskNode => 0;
	public int ColumnNumberOfTaskNode => 0;
	public string ProjectFileOfTaskNode => string.Empty;

	public bool BuildProjectFile(
		string projectFileName, string[] targetNames,
		IDictionary globalProperties, IDictionary targetOutputs) => true;

	public void LogErrorEvent(BuildErrorEventArgs e)
	{
	}
	public void LogWarningEvent(BuildWarningEventArgs e)
	{
	}
	public void LogMessageEvent(BuildMessageEventArgs e)
	{
	}
	public void LogCustomEvent(CustomBuildEventArgs e)
	{
	}
}
