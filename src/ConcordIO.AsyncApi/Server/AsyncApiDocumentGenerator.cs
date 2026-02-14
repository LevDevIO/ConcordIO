using System.Dynamic;
using System.Reflection;
using System.Text.Json;

using Neuroglia.AsyncApi;
using Neuroglia.AsyncApi.v3;

using NJsonSchema;
using NJsonSchema.Generation;

namespace ConcordIO.AsyncApi.Server;

/// <summary>
/// Generates AsyncAPI 3.x documents from discovered .NET types.
/// </summary>
/// <remarks>
/// <para>
/// This generator creates AsyncAPI specifications from .NET message types, enabling
/// contract-first or code-first development with messaging systems like MassTransit.
/// </para>
/// <para>
/// The generated document includes:
/// </para>
/// <list type="bullet">
/// <item><description><c>info</c> - Document metadata (title, version)</description></item>
/// <item><description><c>channels</c> - MassTransit URN-format addresses</description></item>
/// <item><description><c>operations</c> - Publish/subscribe operations</description></item>
/// <item><description><c>components/schemas</c> - JSON Schema definitions with <c>x-dotnet-namespace</c> extension</description></item>
/// <item><description><c>components/messages</c> - Message definitions with payload references</description></item>
/// </list>
/// <para>
/// Custom extensions added to schemas:
/// </para>
/// <list type="bullet">
/// <item><description><c>x-dotnet-namespace</c> - Original .NET namespace for proper code generation</description></item>
/// <item><description><c>x-dotnet-type</c> - Fully qualified .NET type name for external type detection</description></item>
/// </list>
/// </remarks>
/// <example>
/// <para>Basic usage:</para>
/// <code>
/// var discoveryService = new TypeDiscoveryService();
/// var types = discoveryService.DiscoverTypes(assembly, patterns);
/// 
/// var generator = new AsyncApiDocumentGenerator();
/// var document = generator.Generate("OrderService.Contracts", "1.0.0", types);
/// 
/// // Serialize to YAML
/// var writer = new AsyncApiDocumentWriter();
/// var yaml = await writer.WriteAsync(document, "yaml");
/// </code>
/// </example>
public class AsyncApiDocumentGenerator
{
	private const string GeneratorName = "ConcordIO.AsyncApi.Server";

	private static readonly Assembly CoreAssembly = typeof(object).Assembly;

	private readonly SystemTextJsonSchemaGeneratorSettings _schemaSettings;

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncApiDocumentGenerator"/> class.
	/// </summary>
	/// <remarks>
	/// Uses NJsonSchema with the following settings:
	/// <list type="bullet">
	/// <item><description>Schema type: JSON Schema</description></item>
	/// <item><description>Flattened inheritance hierarchy</description></item>
	/// </list>
	/// </remarks>
	public AsyncApiDocumentGenerator()
	{
		_schemaSettings = new SystemTextJsonSchemaGeneratorSettings
		{
			SchemaType = SchemaType.JsonSchema,
			FlattenInheritanceHierarchy = true
		};
	}

	/// <summary>
	/// Generates an AsyncAPI 3.x document from discovered .NET types.
	/// </summary>
	/// <param name="title">The document title (typically the assembly or package name).</param>
	/// <param name="version">The document version (e.g., "1.0.0").</param>
	/// <param name="types">The discovered message types to include in the document.</param>
	/// <returns>A fully populated <see cref="V3AsyncApiDocument"/>.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="title"/> or <paramref name="version"/> is null or whitespace.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="types"/> is null.</exception>
	/// <remarks>
	/// <para>
	/// The generation process:
	/// </para>
	/// <list type="number">
	/// <item><description>Collects all types and their dependencies (nested types, property types)</description></item>
	/// <item><description>Generates JSON Schema for each type with namespace extensions</description></item>
	/// <item><description>Creates message definitions referencing the schemas</description></item>
	/// <item><description>Creates channels with MassTransit URN addresses (<c>urn:message:{namespace}:{type}</c>)</description></item>
	/// <item><description>Creates operations based on message kind (Event = Receive, Command = Send)</description></item>
	/// </list>
	/// <para>
	/// Type dependencies are automatically discovered by scanning public properties.
	/// Primitive types and System types are excluded from schema generation.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// var types = new[]
	/// {
	///     new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event),
	///     new DiscoveredType(typeof(CreateOrderCommand), MessageKind.Command)
	/// };
	/// 
	/// var generator = new AsyncApiDocumentGenerator();
	/// var document = generator.Generate("OrderService", "1.0.0", types);
	/// 
	/// // Access generated content
	/// Console.WriteLine($"Schemas: {document.Components?.Schemas?.Count}");
	/// Console.WriteLine($"Channels: {document.Channels.Count}");
	/// </code>
	/// </example>
	public V3AsyncApiDocument Generate(string title, string version, IEnumerable<DiscoveredType> types)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(title);
		ArgumentException.ThrowIfNullOrWhiteSpace(version);
		ArgumentNullException.ThrowIfNull(types);

		var typeList = types.ToList();

		var document = new V3AsyncApiDocument
		{
			AsyncApi = AsyncApiSpecVersion.V3,
			Info = new V3ApiInfo
			{
				Title = title,
				Version = version,
				Description = $"Generated by {GeneratorName}"
			},
			Channels = [],
			Operations = [],
			Components = new V3ComponentDefinitionCollection
			{
				Messages = [],
				Schemas = []
			}
		};

		// Track all schemas we need to generate (including referenced types)
		var schemasToGenerate = new Dictionary<string, (Type Type, string Namespace)>();

		// First pass: collect all message types and their dependencies
		foreach (var discoveredType in typeList)
		{
			CollectTypeAndDependencies(discoveredType.Type, schemasToGenerate);
		}

		// Generate all schemas
		foreach (var (schemaName, (type, ns)) in schemasToGenerate)
		{
			var schema = GenerateSchema(type, ns);
			document.Components.Schemas![schemaName] = schema;
		}

		// Generate channels, messages, and operations for message types
		foreach (var discoveredType in typeList)
		{
			var type = discoveredType.Type;
			var kind = discoveredType.Kind;
			var typeName = type.Name;
			var fullTypeName = type.FullName ?? typeName;
			var ns = type.Namespace ?? string.Empty;

			// Create message definition
			var message = new V3MessageDefinition
			{
				Name = typeName,
				Title = typeName,
				ContentType = "application/json",
				Payload = new V3SchemaDefinition
				{
					Reference = $"#/components/schemas/{fullTypeName}"
				}
			};
			document.Components.Messages![typeName] = message;

			// Create channel (MassTransit URN format)
			var channelAddress = $"urn:message:{ns}:{typeName}";
			var channel = new V3ChannelDefinition
			{
				Address = channelAddress,
				Messages = new()
				{
					[typeName] = new V3MessageDefinition
					{
						Reference = $"#/components/messages/{typeName}"
					}
				}
			};
			document.Channels[fullTypeName] = channel;

			// Create operation based on message kind
			var operationAction = kind == MessageKind.Event
				? V3OperationAction.Receive  // Events are received by subscribers
				: V3OperationAction.Send;    // Commands are sent to handlers

			var operation = new V3OperationDefinition
			{
				Action = operationAction,
				Channel = new V3ReferenceDefinition
				{
					Reference = $"#/channels/{fullTypeName}"
				},
				Messages =
				[
					new V3ReferenceDefinition
							{
								Reference = $"#/channels/{fullTypeName}/messages/{typeName}"
							}
				]
			};
			document.Operations[$"{typeName}Operation"] = operation;
		}

		return document;
	}

	private void CollectTypeAndDependencies(Type type, Dictionary<string, (Type Type, string Namespace)> schemas)
	{
		var typeName = type.Name;
		var fullTypeName = type.FullName ?? typeName;
		var ns = type.Namespace ?? string.Empty;

		// Skip if already processed or if it's a primitive/system type
		// Use fully-qualified type name to prevent collisions across namespaces
		if (schemas.ContainsKey(fullTypeName) || IsSimpleType(type))
		{
			return;
		}

		schemas[fullTypeName] = (type, ns);

		// Collect dependencies from properties
		foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			var propertyType = property.PropertyType;

			// Handle all generic types - collect all type arguments as potential dependencies
			if (propertyType.IsGenericType)
			{
				var genericArgs = propertyType.GetGenericArguments();
				foreach (var argType in genericArgs)
				{
					if (!IsSimpleType(argType) && argType.Namespace?.StartsWith("System") != true)
					{
						CollectTypeAndDependencies(argType, schemas);
					}
				}

				// Also handle nested collection types (e.g., List<CustomType>)
				// by calling GetUnderlyingType for the outer generic
				var underlyingType = GetUnderlyingType(propertyType);
				if (underlyingType != propertyType &&
					!IsSimpleType(underlyingType) &&
					underlyingType.Namespace?.StartsWith("System") != true)
				{
					CollectTypeAndDependencies(underlyingType, schemas);
				}
				continue;
			}

			var underlyingType2 = GetUnderlyingType(propertyType);
			if (!IsSimpleType(underlyingType2) && underlyingType2.Namespace?.StartsWith("System") != true)
			{
				CollectTypeAndDependencies(underlyingType2, schemas);
			}
		}
	}

	private static Type GetUnderlyingType(Type type)
	{
		// Handle nullable types
		var nullableUnderlyingType = Nullable.GetUnderlyingType(type);
		if (nullableUnderlyingType is not null)
		{
			return nullableUnderlyingType;
		}

		// Handle single-parameter collection types (List<T>, IEnumerable<T>, etc.)
		if (type.IsGenericType)
		{
			var genericArgs = type.GetGenericArguments();
			if (genericArgs.Length == 1)
			{
				var genericDef = type.GetGenericTypeDefinition();
				if (genericDef == typeof(List<>) ||
					genericDef == typeof(IList<>) ||
					genericDef == typeof(ICollection<>) ||
					genericDef == typeof(IEnumerable<>) ||
					genericDef == typeof(HashSet<>))
				{
					return genericArgs[0];
				}
			}
		}

		// Handle arrays
		if (type.IsArray)
		{
			return type.GetElementType() ?? type;
		}

		return type;
	}

	private static bool IsSimpleType(Type type) => type.Assembly == CoreAssembly || type.IsEnum;


	private V3SchemaDefinition GenerateSchema(Type type, string ns)
	{
		// Use NJsonSchema to generate the JSON Schema
		var generator = new JsonSchemaGenerator(_schemaSettings);
		var jsonSchema = generator.Generate(type);

		// Convert NJsonSchema to a dynamic object for the Schema property
		var schemaJson = jsonSchema.ToJson();
		var schemaObject = JsonSerializer.Deserialize<ExpandoObject>(schemaJson);

		// Add our custom extension properties
		if (schemaObject is IDictionary<string, object?> dict)
		{
			dict[AsyncApiConstants.DotNetNamespace] = ns;
			dict[AsyncApiConstants.DotNetType] = type.FullName ?? type.Name;

			// Convert $ref in definitions to components/schemas format
			ConvertReferences(dict);
		}

		return new V3SchemaDefinition
		{
			SchemaFormat = "application/schema+json;version=draft-07",
			Schema = schemaObject!
		};
	}

	private static void ConvertReferences(IDictionary<string, object?> dict)
	{
		foreach (var key in dict.Keys.ToList())
		{
			if (key == "$ref" && dict[key] is string refValue)
			{
				// Convert #/definitions/TypeName to #/components/schemas/TypeName
				dict[key] = refValue.Replace("#/definitions/", "#/components/schemas/");
			}
			else if (dict[key] is IDictionary<string, object?> nestedDict)
			{
				ConvertReferences(nestedDict);
			}
			else if (dict[key] is IList<object?> list)
			{
				foreach (var item in list)
				{
					if (item is IDictionary<string, object?> itemDict)
					{
						ConvertReferences(itemDict);
					}
				}
			}
		}
	}
}
