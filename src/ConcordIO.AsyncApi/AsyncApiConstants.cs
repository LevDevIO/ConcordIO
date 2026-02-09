namespace ConcordIO.AsyncApi;

/// <summary>
/// Defines constants for AsyncAPI extension keys used in document generation and code generation.
/// </summary>
public static class AsyncApiConstants
{
    /// <summary>
    /// Extension key for storing the .NET namespace of a type.
    /// Used by both Server (document generation) and Client (code generation) to preserve namespaces.
    /// </summary>
    public const string DotNetNamespace = "x-dotnet-namespace";

    /// <summary>
    /// Extension key for storing the fully-qualified .NET type name.
    /// Used by both Server (document generation) and Client (code generation) to reference external types.
    /// </summary>
    public const string DotNetType = "x-dotnet-type";
}
