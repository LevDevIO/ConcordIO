namespace ConcordIO.Tool.Services;

/// <summary>
/// Defines constants for supported specification kinds.
/// </summary>
public static class SpecKind
{
	/// <summary>
	/// OpenAPI specification kind.
	/// </summary>
	public const string OpenApi = "openapi";

	/// <summary>
	/// Protocol Buffer specification kind.
	/// </summary>
	public const string Proto = "proto";

	/// <summary>
	/// AsyncAPI specification kind.
	/// </summary>
	public const string AsyncApi = "asyncapi";

	/// <summary>
	/// All supported specification kinds (immutable).
	/// </summary>
	public static readonly IReadOnlyList<string> All = [OpenApi, Proto, AsyncApi];
}
