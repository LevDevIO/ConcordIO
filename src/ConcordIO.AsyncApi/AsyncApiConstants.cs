namespace ConcordIO.AsyncApi;

/// <summary>
/// Defines constants for AsyncAPI extension keys used throughout ConcordIO.
/// </summary>
/// <remarks>
/// <para>
/// These extension keys enable round-trip fidelity between .NET types and AsyncAPI documents.
/// They are used by both the Server (document generation) and Client (code generation) components.
/// </para>
/// <para>
/// Extension keys follow the AsyncAPI specification for custom extensions (prefixed with <c>x-</c>).
/// </para>
/// </remarks>
/// <example>
/// <para>In an AsyncAPI document:</para>
/// <code>
/// components:
///   schemas:
///     OrderCreatedEvent:
///       type: object
///       x-dotnet-namespace: "MyService.Contracts.Events"
///       x-dotnet-type: "MyService.Contracts.Events.OrderCreatedEvent"
///       properties:
///         orderId:
///           type: string
///           format: uuid
/// </code>
/// </example>
public static class AsyncApiConstants
{
	/// <summary>
	/// Extension key for storing the .NET namespace of a type.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Value: <c>"x-dotnet-namespace"</c>
	/// </para>
	/// <para>
	/// Used by:
	/// </para>
	/// <list type="bullet">
	/// <item><description><b>Server</b>: Added to schemas during document generation to preserve the original namespace</description></item>
	/// <item><description><b>Client</b>: Read during code generation to place types in correct namespaces</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Server: Reading namespace for schema generation
	/// var ns = type.Namespace ?? string.Empty;
	/// schema[AsyncApiConstants.DotNetNamespace] = ns;
	/// 
	/// // Client: Reading namespace during code generation
	/// if (schema.TryGetProperty(AsyncApiConstants.DotNetNamespace, out var nsElement))
	/// {
	///     var targetNamespace = nsElement.GetString();
	/// }
	/// </code>
	/// </example>
	public const string DotNetNamespace = "x-dotnet-namespace";

	/// <summary>
	/// Extension key for storing the fully-qualified .NET type name.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Value: <c>"x-dotnet-type"</c>
	/// </para>
	/// <para>
	/// Used by:
	/// </para>
	/// <list type="bullet">
	/// <item><description><b>Server</b>: Added to schemas to enable external type detection</description></item>
	/// <item><description><b>Client</b>: Used to check if a type already exists in referenced assemblies</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Server: Adding type info to schema
	/// schema[AsyncApiConstants.DotNetType] = type.FullName ?? type.Name;
	/// 
	/// // Client: Checking for external type
	/// if (schema.TryGetProperty(AsyncApiConstants.DotNetType, out var typeElement))
	/// {
	///     var fullTypeName = typeElement.GetString();
	///     if (externalTypeResolver.TypeExists(fullTypeName))
	///     {
	///         // Skip generation - type exists externally
	///     }
	/// }
	/// </code>
	/// </example>
	public const string DotNetType = "x-dotnet-type";
}
