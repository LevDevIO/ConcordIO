namespace ConcordIO.Tool.Services;

/// <summary>
/// Service for interacting with NuGet packages via the NuGet CLI.
/// </summary>
/// <remarks>
/// <para>
/// This service wraps NuGet CLI commands for package operations. It requires
/// the <c>nuget</c> command to be available in the system PATH.
/// </para>
/// <para>
/// Used by:
/// </para>
/// <list type="bullet">
/// <item><description><c>breaking</c> command - Downloads published packages for comparison</description></item>
/// <item><description><c>get-spec</c> command - Extracts specs from existing packages</description></item>
/// </list>
/// </remarks>
/// <example>
/// <para>Downloading a package:</para>
/// <code>
/// var nugetService = new NuGetService();
/// 
/// // Download latest version
/// var exitCode = await nugetService.DownloadPackageAsync(
///     outputDir: "packages",
///     packageId: "MyService.Contracts",
///     version: null,
///     prerelease: false
/// );
/// 
/// // Download specific version
/// var exitCode2 = await nugetService.DownloadPackageAsync(
///     outputDir: "packages",
///     packageId: "MyService.Contracts",
///     version: "1.0.0",
///     prerelease: false
/// );
/// </code>
/// </example>
public interface INuGetService
{
	/// <summary>
	/// Downloads a NuGet package to the specified directory.
	/// </summary>
	/// <param name="outputDir">The directory to download and extract the package to.</param>
	/// <param name="packageId">The NuGet package ID to download (e.g., <c>"MyService.Contracts"</c>).</param>
	/// <param name="version">The specific version to download, or <c>null</c> to download the latest version.</param>
	/// <param name="prerelease">If <c>true</c>, includes prerelease versions when resolving latest.</param>
	/// <returns>
	/// The exit code from the <c>nuget install</c> command:
	/// <list type="bullet">
	/// <item><description><c>0</c> - Success</description></item>
	/// <item><description>Non-zero - Failure (package not found, network error, etc.)</description></item>
	/// </list>
	/// </returns>
	/// <remarks>
	/// <para>
	/// The package is downloaded using <c>nuget install</c> which:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Creates a folder named <c>{PackageId}.{Version}</c> in the output directory</description></item>
	/// <item><description>Extracts the package contents including <c>.nuspec</c> and content files</description></item>
	/// <item><description>Does not modify any project files</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// var exitCode = await nugetService.DownloadPackageAsync(
	///     outputDir: tempDir,
	///     packageId: "OrderService.Contracts",
	///     version: "1.2.3",
	///     prerelease: true
	/// );
	/// 
	/// if (exitCode == 0)
	/// {
	///     // Package downloaded to: {tempDir}/OrderService.Contracts.1.2.3/
	/// }
	/// </code>
	/// </example>
	Task<int> DownloadPackageAsync(string outputDir, string packageId, string? version, bool prerelease);
}
