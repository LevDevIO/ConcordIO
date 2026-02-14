using Neuroglia.AsyncApi.v3;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;
using System.Text;
using System.Text.Json;

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

        // Collect all types from schemas
        var typesToProcess = new Dictionary<string, (string Namespace, object Schema)>(StringComparer.Ordinal);

        foreach (var (name, schemaDef) in schemas)
        {
            var ns = GetNamespaceFromExtension(schemaDef.Schema);
            typesToProcess[name] = (ns, schemaDef.Schema);
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

    private static string ExtractClassDefinition(string generatedCode, string typeName)
    {
        // NJsonSchema generates a full file with namespace and usings
        // We need to extract just the class/record definition
        var lines = generatedCode.Split('\n');
        var sb = new StringBuilder();
        var inClass = false;
        var braceCount = 0;
        var foundOpeningBrace = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            // Skip using statements and namespace declarations
            if (trimmed.StartsWith("using ") ||
                trimmed.StartsWith("namespace ") ||
                trimmed.StartsWith("#pragma ") ||
                (trimmed.StartsWith("//") && !inClass))
            {
                continue;
            }

            // Skip empty lines before we've started capturing
            if (!inClass && string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            // Start capturing when we hit the class/record definition
            if (!inClass && (trimmed.StartsWith("public class ") ||
                            trimmed.StartsWith("public partial class ") ||
                            trimmed.StartsWith("public record ") ||
                            trimmed.StartsWith("public sealed class ") ||
                            trimmed.StartsWith("[System.")))  // Also capture attributes
            {
                inClass = true;
            }

            if (inClass)
            {
                sb.AppendLine(line.TrimEnd());

                // Check for single-line record without braces (e.g., "public record Foo(int X);")
                if (trimmed.Contains("record") && trimmed.TrimEnd().EndsWith(";") && !trimmed.Contains("{"))
                {
                    // This is a single-line record definition, we're done
                    break;
                }

                // Track braces to know when the class ends
                braceCount += line.Count(c => c == '{');
                braceCount -= line.Count(c => c == '}');

                // Mark that we've found at least one opening brace
                if (line.Contains('{'))
                {
                    foundOpeningBrace = true;
                }

                // Only break when we've found the opening brace and matched all braces
                if (foundOpeningBrace && braceCount == 0 && sb.Length > 0)
                {
                    break;
                }
            }
        }

        var result = sb.ToString().Trim();

        // If extraction failed or we have an incomplete class, generate a simple POCO
        if (string.IsNullOrEmpty(result) || (!result.Contains('{') && !result.Contains("record")) || (!result.Contains('}') && !result.TrimEnd().EndsWith(";")))
        {
            return $"public partial class {typeName}\n{{\n}}";
        }

        return result;
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
}
