using System.Text;
using System.Text.Json;

using Neuroglia.AsyncApi.v3;

using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;

namespace ConcordIO.AsyncApi.Client;

/// <summary>
/// Generates C# contract types from AsyncAPI 3.x specifications.
/// </summary>
/// <remarks>
/// <para>
/// This generator processes AsyncAPI documents and produces strongly-typed C# classes
/// that can be used with messaging frameworks like MassTransit. It handles:
/// </para>
/// <list type="bullet">
/// <item><description>Proper namespace organization based on <c>x-dotnet-namespace</c> extension</description></item>
/// <item><description>Cross-references between types in different namespaces</description></item>
/// <item><description>Detection of external types to avoid duplicate generation</description></item>
/// <item><description>Configurable output styles (POCO vs Record)</description></item>
/// <item><description>Data annotation attributes for validation</description></item>
/// <item><description>Nullable reference type annotations</description></item>
/// </list>
/// <para>
/// The generator uses NJsonSchema under the hood for schema-to-C# conversion.
/// </para>
/// </remarks>
/// <example>
/// <para>Basic usage:</para>
/// <code>
/// // Parse an AsyncAPI document
/// var document = await AsyncApiDocumentParser.ParseAsync("api.yaml");
/// 
/// // Generate contracts with default settings
/// var generator = new AsyncApiContractGenerator();
/// var result = generator.Generate(document);
/// 
/// // Write generated files
/// foreach (var file in result.SourceFiles)
/// {
///     File.WriteAllText(file.FileName, file.Content);
/// }
/// </code>
/// <para>With custom settings and external type detection:</para>
/// <code>
/// var settings = new ContractGeneratorSettings(
///     GenerateDataAnnotations: true,
///     ClassStyle: GeneratedClassStyle.Record
/// );
/// 
/// var resolver = new ExternalTypeResolver();
/// resolver.LoadAssemblies(new[] { "SharedContracts.dll" });
/// 
/// var generator = new AsyncApiContractGenerator(settings, resolver);
/// var result = generator.Generate(document);
/// 
/// // Types from SharedContracts.dll will be referenced, not regenerated
/// Console.WriteLine($"External types: {result.ExternalTypes.Count}");
/// Console.WriteLine($"Generated types: {result.GeneratedTypes.Count}");
/// </code>
/// </example>
public class AsyncApiContractGenerator
{

	private readonly ContractGeneratorSettings _settings;
	private readonly ExternalTypeResolver _externalTypeResolver;

	/// <summary>
	/// Creates a new contract generator with default settings.
	/// </summary>
	/// <remarks>
	/// Default settings include:
	/// <list type="bullet">
	/// <item><description>Data annotations enabled</description></item>
	/// <item><description>Nullable reference types enabled</description></item>
	/// <item><description>POCO class style</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// var generator = new AsyncApiContractGenerator();
	/// var result = generator.Generate(asyncApiDocument);
	/// </code>
	/// </example>
	public AsyncApiContractGenerator()
		: this(new ContractGeneratorSettings(), new ExternalTypeResolver())
	{
	}

	/// <summary>
	/// Creates a new contract generator with the specified settings and external type resolver.
	/// </summary>
	/// <param name="settings">Generator settings controlling output format, annotations, and type mappings.</param>
	/// <param name="externalTypeResolver">Resolver for detecting types that exist in referenced assemblies.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> or <paramref name="externalTypeResolver"/> is null.</exception>
	/// <example>
	/// <code>
	/// var settings = new ContractGeneratorSettings(
	///     GenerateDataAnnotations: true,
	///     GenerateNullableReferenceTypes: true,
	///     ClassStyle: GeneratedClassStyle.Record
	/// );
	/// 
	/// var resolver = new ExternalTypeResolver();
	/// resolver.LoadAssemblies(Directory.GetFiles("libs", "*.dll"));
	/// 
	/// var generator = new AsyncApiContractGenerator(settings, resolver);
	/// </code>
	/// </example>
	public AsyncApiContractGenerator(ContractGeneratorSettings settings, ExternalTypeResolver externalTypeResolver)
	{
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));
		_externalTypeResolver = externalTypeResolver ?? throw new ArgumentNullException(nameof(externalTypeResolver));
	}

	/// <summary>
	/// Generates C# contract types from an AsyncAPI document.
	/// </summary>
	/// <param name="document">The AsyncAPI 3.x document containing schemas to generate.</param>
	/// <returns>
	/// A <see cref="ContractGenerationResult"/> containing:
	/// <list type="bullet">
	/// <item><description><see cref="ContractGenerationResult.SourceFiles"/> - Generated C# source files grouped by namespace</description></item>
	/// <item><description><see cref="ContractGenerationResult.ExternalTypes"/> - Types found in referenced assemblies (not generated)</description></item>
	/// <item><description><see cref="ContractGenerationResult.GeneratedTypes"/> - All types that were generated</description></item>
	/// </list>
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
	/// <remarks>
	/// <para>
	/// The generation process:
	/// </para>
	/// <list type="number">
	/// <item><description>Extracts all schemas from <c>components/schemas</c></description></item>
	/// <item><description>Reads <c>x-dotnet-namespace</c> extension for namespace assignment</description></item>
	/// <item><description>Checks each type against the external type resolver</description></item>
	/// <item><description>Generates C# code for types not found externally</description></item>
	/// <item><description>Groups output by namespace into separate <c>.g.cs</c> files</description></item>
	/// </list>
	/// <para>
	/// Generated files include:
	/// </para>
	/// <list type="bullet">
	/// <item><description><c>&lt;auto-generated&gt;</c> header comment</description></item>
	/// <item><description><c>#nullable enable</c> directive</description></item>
	/// <item><description>Appropriate using statements</description></item>
	/// <item><description>File-scoped namespace declaration</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// var generator = new AsyncApiContractGenerator();
	/// var result = generator.Generate(document);
	/// 
	/// foreach (var sourceFile in result.SourceFiles)
	/// {
	///     Console.WriteLine($"Generated {sourceFile.FileName}:");
	///     Console.WriteLine($"  Namespace: {sourceFile.Namespace}");
	///     Console.WriteLine($"  Types: {string.Join(", ", sourceFile.Types.Select(t => t.TypeName))}");
	/// }
	/// </code>
	/// </example>
	public ContractGenerationResult Generate(V3AsyncApiDocument document)
	{
		ArgumentNullException.ThrowIfNull(document);

		var schemas = document.Components?.Schemas ?? [];
		var messages = document.Components?.Messages ?? [];

		// Collect all top-level types from schemas.
		// NOTE: We intentionally do NOT extract types from each schema's "definitions" section.
		// Definition types (e.g., RateSyncConfigId) that are referenced via $ref within schemas
		// should already exist as separate top-level schemas in the AsyncAPI document if they need
		// their own generated type. Extracting definitions would break $ref resolution because
		// the extracted definition loses the parent schema's definitions context, causing
		// NJsonSchema to fail with "Could not resolve the path '#/definitions/...'" errors.
		// NJsonSchema generates definition types inline when processing the parent schema,
		// and ExtractClassDefinition filters the output to only the target type.
		var typesToProcess = new Dictionary<string, (string Namespace, object Schema)>(StringComparer.Ordinal);

		foreach (var (name, schemaDef) in schemas)
		{
			var ns = GetNamespaceFromExtension(schemaDef.Schema);

			// Use the short type name (last segment after the last dot) as the key.
			// Schema keys are fully-qualified names like "Contoso.Application.RateSync.Messages.RateSyncCompleted"
			// but the generated C# class name should be just "DhlRateSyncCompleted".
			var shortName = GetShortTypeName(name);
			if (!typesToProcess.ContainsKey(shortName))
			{
				typesToProcess[shortName] = (ns, schemaDef.Schema);
			}

			foreach (var (definitionName, definitionSchema) in ExtractEnumDefinitions(schemaDef.Schema))
			{
				if (!typesToProcess.ContainsKey(definitionName))
				{
					typesToProcess[definitionName] = (ns, definitionSchema);
				}
			}
		}

		// Determine which types are external vs need generation
		var externalTypes = new List<TypeInfo>();
		var typesToGenerate = new Dictionary<string, (string Namespace, object Schema)>(StringComparer.Ordinal);

		foreach (var (name, (ns, schema)) in typesToProcess)
		{
			var fullTypeName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
			var externalInfo = _externalTypeResolver.GetExternalTypeInfo(fullTypeName);

			if (externalInfo is not null)
			{
				externalTypes.Add(externalInfo);
			}
			else
			{
				typesToGenerate[name] = (ns, schema);
			}
		}

		// Group by namespace for file generation
		var byNamespace = typesToGenerate
			.GroupBy(kvp => kvp.Value.Namespace)
			.ToDictionary(g => g.Key, g => g.ToList());

		var sourceFiles = new List<GeneratedSourceFile>();
		var generatedTypes = new List<TypeInfo>();

		// Generate a file per namespace
		foreach (var (ns, types) in byNamespace)
		{
			var (fileName, content, typeInfos) = GenerateNamespaceFile(ns, types, byNamespace.Keys, externalTypes);
			sourceFiles.Add(new GeneratedSourceFile(fileName, ns, content, typeInfos));
			generatedTypes.AddRange(typeInfos);
		}

		return new ContractGenerationResult(sourceFiles, externalTypes, generatedTypes);
	}

	private (string FileName, string Content, List<TypeInfo> Types) GenerateNamespaceFile(
		string ns,
		List<KeyValuePair<string, (string Namespace, object Schema)>> types,
		IEnumerable<string> allNamespaces,
		List<TypeInfo> externalTypes)
	{
		var sb = new StringBuilder();
		var typeInfos = new List<TypeInfo>();

		// Determine required using statements
		var usings = new HashSet<string>(StringComparer.Ordinal);

		// Add system usings based on settings
		usings.Add("System");
		if (_settings.GenerateDataAnnotations)
		{
			usings.Add("System.ComponentModel.DataAnnotations");
		}
		usings.Add("System.Collections.Generic");

		// Add usings for other namespaces in this document
		foreach (var otherNs in allNamespaces)
		{
			if (!string.IsNullOrEmpty(otherNs) && otherNs != ns)
			{
				usings.Add(otherNs);
			}
		}

		// Add usings for external types
		foreach (var ext in externalTypes)
		{
			if (!string.IsNullOrEmpty(ext.Namespace) && ext.Namespace != ns)
			{
				usings.Add(ext.Namespace);
			}
		}

		// Write file header
		sb.AppendLine("// <auto-generated>");
		sb.AppendLine("//     This code was generated by ConcordIO.AsyncApi.Client.");
		sb.AppendLine("//     Do not modify this file directly.");
		sb.AppendLine("// </auto-generated>");
		sb.AppendLine();
		sb.AppendLine("#nullable enable");
		sb.AppendLine();

		// Write using statements
		foreach (var u in usings.OrderBy(u => u))
		{
			sb.AppendLine($"using {u};");
		}

		sb.AppendLine();

		// Write namespace
		var namespaceToUse = string.IsNullOrEmpty(ns) ? "GeneratedContracts" : ns;
		sb.AppendLine($"namespace {namespaceToUse};");
		sb.AppendLine();

		// Generate each type
		foreach (var (name, (_, schema)) in types)
		{
			var typeCode = GenerateTypeFromSchema(name, schema);
			sb.AppendLine(typeCode);
			sb.AppendLine();

			typeInfos.Add(new TypeInfo(name, namespaceToUse));
		}

		var fileName = $"{namespaceToUse}.g.cs";
		return (fileName, sb.ToString(), typeInfos);
	}

	private string GenerateTypeFromSchema(string typeName, object schema)
	{
		// Convert the schema to a JsonSchema for NJsonSchema code generation
		var jsonSchema = ConvertToJsonSchema(schema);

		// NOTE: We intentionally keep jsonSchema.Definitions intact.
		// NJsonSchema needs them to resolve $ref references (e.g., #/definitions/RateSyncConfigId).
		// ExtractClassDefinition filters the NJsonSchema output to only the target type,
		// skipping any definition types that NJsonSchema generates inline.

		// Configure CSharp generator settings
		var csharpSettings = new CSharpGeneratorSettings
		{
			ClassStyle = _settings.ClassStyle == GeneratedClassStyle.Record
				? CSharpClassStyle.Record
				: CSharpClassStyle.Poco,
			GenerateDataAnnotations = _settings.GenerateDataAnnotations,
			GenerateNullableReferenceTypes = _settings.GenerateNullableReferenceTypes,
			DateType = _settings.DateType,
			DateTimeType = _settings.DateTimeType,
			TimeType = _settings.TimeType,
			TimeSpanType = _settings.TimeSpanType,
			ArrayType = _settings.ArrayType,
			DictionaryType = _settings.DictionaryType,
			Namespace = string.Empty, // We handle namespace ourselves
			GenerateJsonMethods = false,
			GenerateDefaultValues = true,
			JsonLibrary = CSharpJsonLibrary.SystemTextJson // Use System.Text.Json instead of Newtonsoft
		};

		// Generate the type using NJsonSchema
		var generator = new CSharpGenerator(jsonSchema, csharpSettings);
		var code = generator.GenerateFile(typeName);

		// Extract just the class definition, removing namespace wrapper and usings that NJsonSchema adds
		return ExtractClassDefinition(code, typeName);
	}

	private static JsonSchema ConvertToJsonSchema(object schema)
	{
		// The schema from AsyncAPI could be a JsonElement or a dictionary
		// We need to serialize it and parse as JsonSchema
		string jsonString;

		if (schema is JsonElement jsonElement)
		{
			jsonString = jsonElement.GetRawText();
		}
		else
		{
			jsonString = JsonSerializer.Serialize(schema);
		}

		// NJsonSchema provides FromJsonAsync but not a sync version
		// Since we're in a sync context and this is a CPU-bound parsing operation,
		// we use GetAwaiter().GetResult() which is acceptable here
		return JsonSchema.FromJsonAsync(jsonString).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Extracts only the class definition matching <paramref name="typeName"/> from NJsonSchema output.
	/// </summary>
	/// <param name="generatedCode">The full file output from NJsonSchema's <c>GenerateFile</c>.</param>
	/// <param name="typeName">The specific type name to extract (e.g., "StartDhlRateSync").</param>
	/// <returns>The C# class/record definition for the requested type only.</returns>
	/// <remarks>
	/// <para>
	/// NJsonSchema generates ALL types in a schema file, including types from the
	/// <c>definitions</c> section. When multiple message schemas reference the same
	/// definition type (e.g., <c>RateSyncConfigId</c>), that definition appears in
	/// the generated output of every parent schema.
	/// </para>
	/// <para>
	/// This method solves the duplication problem by extracting ONLY the class whose
	/// name matches <paramref name="typeName"/>. Definition types are generated
	/// separately as top-level schemas.
	/// </para>
	/// </remarks>
	private static string ExtractClassDefinition(string generatedCode, string typeName)
	{
		// NJsonSchema generates a full file with namespace, usings, and potentially
		// multiple type declarations (the main type + definition types). We need to
		// find and extract ONLY the declaration matching typeName.
		var lines = generatedCode.Split('\n');
		var sb = new StringBuilder();
		var inClass = false;
		var braceCount = 0;
		var foundOpeningBrace = false;
		var pendingAttribute = new StringBuilder();

		for (var i = 0; i < lines.Length; i++)
		{
			var trimmed = lines[i].TrimStart();

			// Skip using statements, namespace declarations, pragmas, and top-level comments
			if (trimmed.StartsWith("using ") ||
				trimmed.StartsWith("namespace ") ||
				trimmed.StartsWith("#pragma ") ||
				(trimmed.StartsWith("//") && !inClass))
			{
				continue;
			}

			// Skip empty lines when not inside the target class
			if (!inClass && string.IsNullOrWhiteSpace(trimmed))
			{
				continue;
			}

			if (!inClass)
			{
				// Buffer attribute lines (they precede the class declaration)
				if (trimmed.StartsWith("["))
				{
					pendingAttribute.AppendLine(lines[i].TrimEnd());
					continue;
				}

				// Check if this line declares the type we're looking for
				if (IsTypeDeclaration(trimmed) && IsTargetType(trimmed, typeName))
				{
					inClass = true;
					// Include any buffered attributes
					if (pendingAttribute.Length > 0)
					{
						sb.Append(pendingAttribute);
					}
				}
				else if (IsTypeDeclaration(trimmed))
				{
					// This is a different declaration (e.g., a definition type) — skip it and its body
					pendingAttribute.Clear();
					SkipClassBody(lines, ref i);
					continue;
				}
				else
				{
					// Not a class or attribute — discard buffered attributes
					pendingAttribute.Clear();
					continue;
				}
			}

			if (inClass)
			{
				sb.AppendLine(lines[i].TrimEnd());

				// Handle single-line record: "public record Foo(int X);"
				if (trimmed.Contains("record") && trimmed.TrimEnd().EndsWith(";") && !trimmed.Contains("{"))
				{
					break;
				}

				// Track braces to find the end of the class body
				braceCount += lines[i].Count(c => c == '{');
				braceCount -= lines[i].Count(c => c == '}');

				if (lines[i].Contains('{'))
				{
					foundOpeningBrace = true;
				}

				if (foundOpeningBrace && braceCount == 0)
				{
					break;
				}
			}
		}

		var result = sb.ToString().Trim();

		// If extraction failed, generate a minimal POCO so compilation doesn't break
		if (string.IsNullOrEmpty(result) ||
			(!result.Contains('{') && !result.Contains("record")) ||
			(!result.Contains('}') && !result.TrimEnd().EndsWith(";")))
		{
			return $"public partial class {typeName}\n{{\n}}";
		}

		return result;
	}

	/// <summary>
	/// Determines whether a trimmed source line is a class, record, or enum declaration.
	/// </summary>
	private static bool IsTypeDeclaration(string trimmedLine)
	{
		return trimmedLine.StartsWith("public class ") ||
			   trimmedLine.StartsWith("public partial class ") ||
			   trimmedLine.StartsWith("public record ") ||
			   trimmedLine.StartsWith("public sealed class ") ||
			   trimmedLine.StartsWith("public enum ");
	}

	/// <summary>
	/// Checks whether a class declaration line declares the target type by name.
	/// </summary>
	/// <param name="trimmedLine">The trimmed source line containing the class declaration.</param>
	/// <param name="typeName">The type name to look for.</param>
	/// <returns><c>true</c> if the line declares a class/record named <paramref name="typeName"/>.</returns>
	private static bool IsTargetType(string trimmedLine, string typeName)
	{
		// After "public [partial] class " or "public record ", the next token is the type name.
		// It may be followed by whitespace, '{', '(', ':', or end of line.
		var searchPatterns = new[]
		{
			$"class {typeName}",
			$"record {typeName}",
			$"enum {typeName}"
		};

		foreach (var pattern in searchPatterns)
		{
			var idx = trimmedLine.IndexOf(pattern, StringComparison.Ordinal);
			if (idx < 0)
				continue;

			var afterName = idx + pattern.Length;
			// The type name must be followed by a delimiter or end of line
			if (afterName >= trimmedLine.Length ||
				trimmedLine[afterName] == ' ' ||
				trimmedLine[afterName] == '{' ||
				trimmedLine[afterName] == '(' ||
				trimmedLine[afterName] == ':' ||
				trimmedLine[afterName] == '\r' ||
				trimmedLine[afterName] == '\n')
			{
				return true;
			}
		}

		return false;
	}

	private static IEnumerable<(string Name, object Schema)> ExtractEnumDefinitions(object schema)
	{
		if (schema is JsonElement element &&
			element.ValueKind == JsonValueKind.Object &&
			element.TryGetProperty("definitions", out var definitionsElement) &&
			definitionsElement.ValueKind == JsonValueKind.Object)
		{
			foreach (var definition in definitionsElement.EnumerateObject())
			{
				if (IsEnumSchema(definition.Value))
				{
					yield return (definition.Name, definition.Value);
				}
			}
			yield break;
		}

		if (schema is IDictionary<string, object> dictionary &&
			dictionary.TryGetValue("definitions", out var definitionsObject) &&
			definitionsObject is IDictionary<string, object> definitions)
		{
			foreach (var (definitionName, definitionSchema) in definitions)
			{
				if (IsEnumSchema(definitionSchema))
				{
					yield return (definitionName, definitionSchema);
				}
			}
		}
	}

	private static bool IsEnumSchema(object schema)
	{
		if (schema is JsonElement element && element.ValueKind == JsonValueKind.Object)
		{
			return element.TryGetProperty("enum", out var enumElement) &&
				enumElement.ValueKind == JsonValueKind.Array &&
				enumElement.GetArrayLength() > 0;
		}

		if (schema is IDictionary<string, object> dictionary &&
			dictionary.TryGetValue("enum", out var enumObject))
		{
			if (enumObject is JsonElement enumElement)
			{
				return enumElement.ValueKind == JsonValueKind.Array && enumElement.GetArrayLength() > 0;
			}

			if (enumObject is IEnumerable<object> enumValues)
			{
				return enumValues.Any();
			}

			if (enumObject is IEnumerable<int> intValues)
			{
				return intValues.Any();
			}
		}

		return false;
	}

	/// <summary>
	/// Advances the line index past the body of a class/record declaration.
	/// </summary>
	/// <remarks>
	/// Used to skip over definition types that we don't want to extract.
	/// Handles both brace-delimited bodies and single-line records ending with <c>;</c>.
	/// </remarks>
	private static void SkipClassBody(string[] lines, ref int i)
	{
		var braceCount = 0;
		var foundBrace = false;

		for (; i < lines.Length; i++)
		{
			var line = lines[i];
			var trimmed = line.TrimStart();

			// Single-line record: "public record Foo(int X);"
			if (!foundBrace && trimmed.Contains("record") && trimmed.TrimEnd().EndsWith(";") && !trimmed.Contains("{"))
			{
				return;
			}

			braceCount += line.Count(c => c == '{');
			braceCount -= line.Count(c => c == '}');

			if (line.Contains('{'))
			{
				foundBrace = true;
			}

			if (foundBrace && braceCount == 0)
			{
				return;
			}
		}
	}

	private static string GetNamespaceFromExtension(object schema)
	{
		// Try to extract x-dotnet-namespace from the schema
		if (schema is JsonElement element && element.ValueKind == JsonValueKind.Object)
		{
			if (element.TryGetProperty(AsyncApiConstants.DotNetNamespace, out var nsElement) &&
				nsElement.ValueKind == JsonValueKind.String)
			{
				return nsElement.GetString() ?? string.Empty;
			}
		}

		if (schema is IDictionary<string, object> dict)
		{
			if (dict.TryGetValue(AsyncApiConstants.DotNetNamespace, out var ns) && ns is string nsString)
			{
				return nsString;
			}
		}

		return string.Empty;
	}

	/// <summary>
	/// Extracts the short type name from a potentially fully-qualified schema key.
	/// </summary>
	/// <param name="schemaKey">
	/// The schema key, which may be a fully-qualified name like
	/// <c>Contoso.Application.RateSync.Messages.RateSyncCompleted</c>
	/// or a simple name like <c>DhlRateSyncCompleted</c>.
	/// </param>
	/// <returns>
	/// The short type name (e.g., <c>DhlRateSyncCompleted</c>).
	/// If the key contains dots, returns the segment after the last dot.
	/// </returns>
	/// <remarks>
	/// AsyncAPI schema keys in <c>components/schemas</c> are typically fully-qualified
	/// .NET type names. The namespace portion is provided separately via the
	/// <c>x-dotnet-namespace</c> extension, so we only need the short class name
	/// for C# code generation.
	/// </remarks>
	private static string GetShortTypeName(string schemaKey)
	{
		var lastDot = schemaKey.LastIndexOf('.');
		return lastDot >= 0 ? schemaKey[(lastDot + 1)..] : schemaKey;
	}

}
