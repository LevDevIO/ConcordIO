using ConcordIO.Tool.AOComparison;

namespace ConcordIO.Tool.Services;

/// <summary>
/// Interface for running oasdiff commands to compare OpenAPI specifications.
/// </summary>
public interface IOasDiffRunner
{
    /// <summary>
    /// Gets breaking changes between two OpenAPI specs.
    /// </summary>
    /// <param name="baseSpec">Path to the base specification file.</param>
    /// <param name="revisionSpec">Path to the revision specification file.</param>
    /// <param name="arguments">Additional command line arguments.</param>
    /// <returns>Result containing exit code, output, and error streams.</returns>
    Task<OasDiffResult> Breaking(string baseSpec, string revisionSpec, string arguments);

    /// <summary>
    /// Runs an arbitrary oasdiff command.
    /// </summary>
    /// <param name="arguments">The command line arguments.</param>
    /// <returns>Result containing exit code, output, and error streams.</returns>
    Task<OasDiffResult> Run(string arguments);
}
