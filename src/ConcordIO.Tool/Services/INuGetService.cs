namespace ConcordIO.Tool.Services;

/// <summary>
/// Service for interacting with NuGet packages.
/// </summary>
public interface INuGetService
{
    /// <summary>
    /// Downloads a NuGet package to the specified directory.
    /// </summary>
    /// <param name="outputDir">The directory to download the package to.</param>
    /// <param name="packageId">The package ID to download.</param>
    /// <param name="version">The specific version to download, or null for latest.</param>
    /// <param name="prerelease">Whether to include prerelease versions.</param>
    /// <returns>Exit code from the nuget command.</returns>
    Task<int> DownloadPackageAsync(string outputDir, string packageId, string? version, bool prerelease);
}
