# ConcordIO.AsyncApi — Architecture

This document explains the internal design of the shared AsyncAPI library. For the design rationale and overall system vision, see [ConcordIO.AsyncApi.Design.md](../../ConcordIO.AsyncApi.Design.md).

## High-Level Overview

ConcordIO.AsyncApi is the shared core library that provides two sets of functionality:

1. **Server-side** — Generate AsyncAPI 3.x documents from .NET types (reflection-based)
2. **Client-side** — Generate C# contract types from AsyncAPI documents (schema-based code generation)

```
┌─────────────────────────────────────────────────────────┐
│  ConcordIO.AsyncApi (shared library)                    │
│                                                         │
│  ┌─────────────────────┐  ┌──────────────────────────┐  │
│  │  Server/             │  │  Client/                  │  │
│  │  ├─ TypeDiscovery    │  │  ├─ ContractGenerator     │  │
│  │  │  Service          │  │  │  (NJsonSchema CSharp)  │  │
│  │  ├─ AsyncApiDocument │  │  ├─ ExternalTypeResolver  │  │
│  │  │  Generator        │  │  ├─ ContractGenerator     │  │
│  │  │  (NJsonSchema)    │  │  │  Settings              │  │
│  │  └─ AsyncApiDocument │  │  └─ TypeInfo / Result     │  │
│  │     Writer           │  │     records               │  │
│  └─────────────────────┘  └──────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
       ▲                              ▲
       │                              │
┌──────┴───────┐            ┌─────────┴──────────┐
│ AsyncApi     │            │ AsyncApi           │
│ .Server      │            │ .Client            │
│ (MSBuild     │            │ (MSBuild           │
│  Task)       │            │  Task)             │
└──────────────┘            └────────────────────┘
```

## Project Structure

```
ConcordIO.AsyncApi/
├── ConcordIO.AsyncApi.csproj     # Shared library project
├── AsyncApiConstants.cs           # Shared extension key constants (x-dotnet-namespace, x-dotnet-type)
│
├── Server/                       # Server-side: .NET types → AsyncAPI
│   ├── TypeDiscoveryService.cs   # Pattern-based type discovery from assemblies
│   ├── AsyncApiDocumentGenerator.cs  # Builds AsyncAPI 3.x document from discovered types
│   └── AsyncApiDocumentWriter.cs # Serializes document to YAML/JSON files
│
└── Client/                       # Client-side: AsyncAPI → C# code
    ├── AsyncApiContractGenerator.cs  # Generates C# types from AsyncAPI schemas
    ├── ExternalTypeResolver.cs   # Detects types in referenced assemblies to skip
    ├── ContractGeneratorSettings.cs  # Code generation configuration
    └── GeneratedTypeInfo.cs      # Result records (TypeInfo, GeneratedSourceFile, etc.)
```

## Server-Side Components

### TypeDiscoveryService

Discovers .NET types from a compiled assembly using pattern matching. Supports five discovery modes:

| Pattern | Mode | Result |
|---------|------|--------|
| `Namespace.*` | Exact namespace wildcard | All public non-abstract types in namespace |
| `Namespace.**` | Recursive wildcard | All types in namespace and sub-namespaces |
| `IMyInterface` | Interface | All implementations |
| `MyBaseClass` | Base class | All subclasses |
| `MyConcreteType` | Concrete type | That specific type |

Each pattern carries a `MessageKind` (Event or Command) which determines the AsyncAPI operation action (receive vs send).

### AsyncApiDocumentGenerator

Builds an AsyncAPI 3.x `V3AsyncApiDocument` from discovered types:

```
DiscoveredType[]
    │
    ▼
CollectTypeAndDependencies()
    │  Walks properties recursively
    │  Handles Nullable<T>, List<T>, Dictionary<K,V>, arrays
    │  Filters out simple types (primitives, string, Guid, DateTime, etc.)
    │
    ▼
GenerateSchema() per type
    │  NJsonSchema generates JSON Schema from .NET type
    │  Adds x-dotnet-namespace and x-dotnet-type extensions
    │  Converts $ref from #/definitions/ to #/components/schemas/
    │
    ▼
Build channels, messages, operations
    │  Channel address: MassTransit URN format (urn:message:{ns}:{type})
    │  Operation action: Receive for Events, Send for Commands
    │
    ▼
V3AsyncApiDocument
```

**Key extension properties:**

Both Server and Client components use the constants defined in `AsyncApiConstants`:

| Constant | Value | Purpose |
|----------|-------|---------|
| `AsyncApiConstants.DotNetNamespace` | `x-dotnet-namespace` | Preserves the original .NET namespace for code generation |
| `AsyncApiConstants.DotNetType` | `x-dotnet-type` | Preserves the fully-qualified .NET type name |

### AsyncApiDocumentWriter

Serializes `V3AsyncApiDocument` to files using Neuroglia serializers:

- `WriteYamlAsync()` — uses `YamlSerializer.Default`
- `WriteJsonAsync()` — uses `JsonSerializer.Default`

## Client-Side Components

### AsyncApiContractGenerator

Generates C# source files from AsyncAPI documents:

```
V3AsyncApiDocument
    │
    ▼
Collect schemas from components.schemas
    │  Extract x-dotnet-namespace for each schema
    │
    ▼
Classify types: external vs. generate
    │  ExternalTypeResolver checks referenced assemblies
    │
    ▼
Group by namespace
    │
    ▼
For each namespace → GenerateNamespaceFile()
    │  Build using statements (cross-namespace + external)
    │  For each type → GenerateTypeFromSchema()
    │    Convert AsyncAPI schema → NJsonSchema JsonSchema
    │    CSharpGenerator produces code with configured settings
    │    ExtractClassDefinition strips namespace/usings from output
    │
    ▼
ContractGenerationResult
    ├── SourceFiles (GeneratedSourceFile[])
    ├── ExternalTypes (TypeInfo[])
    └── GeneratedTypes (TypeInfo[])
```

**Generated file naming:** `{Namespace}.g.cs`

### ExternalTypeResolver

Loads referenced assemblies and caches their exported types by full name. When the generator encounters a schema whose `x-dotnet-type` matches a cached type, it skips generation and adds a `using` statement instead.

### ContractGeneratorSettings

Configurable code generation options:

| Setting | Default | Description |
|---------|---------|-------------|
| `GenerateDataAnnotations` | `true` | Emit `[Required]`, `[StringLength]`, etc. |
| `GenerateNullableReferenceTypes` | `true` | Emit `?` on nullable reference types |
| `GenerateRequiredProperties` | `false` | Emit C# 11 `required` keyword |
| `ClassStyle` | `Poco` | `Poco` or `Record` |
| `DateType` | `System.DateOnly` | .NET type for `format: date` |
| `DateTimeType` | `System.DateTimeOffset` | .NET type for `format: date-time` |
| `TimeType` | `System.TimeOnly` | .NET type for `format: time` |
| `ArrayType` | `System.Collections.Generic.List` | Generic collection type |
| `DictionaryType` | `System.Collections.Generic.Dictionary` | Generic dictionary type |

## Namespace Handling

MassTransit constructs message URNs from the full type name:

```
Type:  MyService.Contracts.Events.OrderCreatedEvent
URN:   urn:message:MyService.Contracts.Events:OrderCreatedEvent
```

The library preserves namespaces through the full round-trip:

1. **Server** stores namespace in `x-dotnet-namespace` extension on schemas and messages
2. **Client** reads `x-dotnet-namespace` to generate types in the correct namespace
3. **Cross-references** between namespaces are handled with `using` statements
