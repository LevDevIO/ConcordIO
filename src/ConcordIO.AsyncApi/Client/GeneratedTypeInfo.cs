namespace ConcordIO.AsyncApi.Client;

/// <summary>
/// Represents information about a type to be generated or referenced.
/// </summary>
/// <param name="TypeName">The simple type name (e.g., "OrderCreatedEvent").</param>
/// <param name="Namespace">The full namespace (e.g., "MyService.Contracts.Events").</param>
/// <param name="IsExternal">Whether this type exists in an external assembly and should not be generated.</param>
/// <param name="ExternalAssembly">If external, the assembly name containing the type.</param>
public record TypeInfo(
	string TypeName,
	string Namespace,
	bool IsExternal = false,
	string? ExternalAssembly = null)
{
	/// <summary>
	/// Gets the fully qualified type name.
	/// </summary>
	public string FullName => string.IsNullOrEmpty(Namespace) ? TypeName : $"{Namespace}.{TypeName}";
}

/// <summary>
/// Represents a generated C# source file.
/// </summary>
/// <param name="FileName">The file name (e.g., "MyService.Contracts.Events.cs").</param>
/// <param name="Namespace">The primary namespace in this file.</param>
/// <param name="Content">The generated C# source code.</param>
/// <param name="Types">The types contained in this file.</param>
public record GeneratedSourceFile(
	string FileName,
	string Namespace,
	string Content,
	IReadOnlyList<TypeInfo> Types);

/// <summary>
/// Result of the contract generation process.
/// </summary>
/// <param name="SourceFiles">The generated source files.</param>
/// <param name="ExternalTypes">Types that were found in external assemblies and not generated.</param>
/// <param name="GeneratedTypes">All types that were generated.</param>
public record ContractGenerationResult(
	IReadOnlyList<GeneratedSourceFile> SourceFiles,
	IReadOnlyList<TypeInfo> ExternalTypes,
	IReadOnlyList<TypeInfo> GeneratedTypes);
