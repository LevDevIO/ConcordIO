namespace ConcordIO.Tool.Services;

/// <summary>
/// Interface for running oasdiff commands to compare OpenAPI specifications.
/// </summary>
/// <remarks>
/// <para>
/// This service wraps the <c>oasdiff</c> CLI tool, which is bundled with ConcordIO
/// as platform-specific native binaries in the <c>AOComparison/oasdiff_bin/</c> folder.
/// </para>
/// <para>
/// Supported platforms:
/// </para>
/// <list type="bullet">
/// <item><description>Windows x64/ARM64</description></item>
/// <item><description>Linux x64/ARM64</description></item>
/// <item><description>macOS (universal)</description></item>
/// </list>
/// <para>
/// Learn more about oasdiff: <see href="https://github.com/Tufin/oasdiff"/>
/// </para>
/// </remarks>
/// <example>
/// <para>Checking for breaking changes:</para>
/// <code>
/// var runner = new OasDiffRunner();
/// var result = await runner.Breaking(
///     baseSpec: "v1.yaml",
///     revisionSpec: "v2.yaml",
///     arguments: "--format json"
/// );
/// 
/// if (result.ExitCode == 0)
/// {
///     Console.WriteLine("No breaking changes detected!");
/// }
/// else
/// {
///     Console.WriteLine($"Breaking changes found:\n{result.Output}");
/// }
/// </code>
/// </example>
public interface IOasDiffRunner
{
	/// <summary>
	/// Detects breaking changes between two OpenAPI specifications.
	/// </summary>
	/// <param name="baseSpec">Path to the base (original) OpenAPI specification file.</param>
	/// <param name="revisionSpec">Path to the revision (new) OpenAPI specification file.</param>
	/// <param name="arguments">Additional command line arguments to pass to oasdiff (e.g., <c>"--format json"</c>).</param>
	/// <returns>
	/// An <see cref="OasDiffResult"/> containing:
	/// <list type="bullet">
	/// <item><description><see cref="OasDiffResult.ExitCode"/> - 0 if no breaking changes, non-zero otherwise</description></item>
	/// <item><description><see cref="OasDiffResult.Output"/> - Standard output from oasdiff</description></item>
	/// <item><description><see cref="OasDiffResult.Error"/> - Standard error from oasdiff</description></item>
	/// </list>
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method runs: <c>oasdiff breaking "{baseSpec}" "{revisionSpec}" -o WARN {arguments}</c>
	/// </para>
	/// <para>
	/// Exit codes:
	/// </para>
	/// <list type="bullet">
	/// <item><description><c>0</c> - No breaking changes detected</description></item>
	/// <item><description><c>1</c> - Breaking changes detected (at WARN level or above)</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// var result = await runner.Breaking(
	///     "published/api.yaml",
	///     "current/api.yaml",
	///     "--format text"
	/// );
	/// Console.WriteLine(result.Output);
	/// </code>
	/// </example>
	Task<OasDiffResult> Breaking(string baseSpec, string revisionSpec, string arguments);

	/// <summary>
	/// Runs an arbitrary oasdiff command with the specified arguments.
	/// </summary>
	/// <param name="arguments">The complete command line arguments for oasdiff.</param>
	/// <returns>An <see cref="OasDiffResult"/> containing exit code, output, and error streams.</returns>
	/// <remarks>
	/// <para>
	/// Use this method for oasdiff commands other than <c>breaking</c>, such as:
	/// </para>
	/// <list type="bullet">
	/// <item><description><c>diff</c> - Show all changes (not just breaking)</description></item>
	/// <item><description><c>changelog</c> - Generate a changelog</description></item>
	/// <item><description><c>flatten</c> - Flatten OpenAPI spec</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Generate a diff report
	/// var result = await runner.Run("diff v1.yaml v2.yaml --format yaml");
	/// 
	/// // Generate changelog
	/// var changelog = await runner.Run("changelog v1.yaml v2.yaml");
	/// </code>
	/// </example>
	Task<OasDiffResult> Run(string arguments);
}
