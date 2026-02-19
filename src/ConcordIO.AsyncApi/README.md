# ConcordIO.AsyncApi

A shared .NET library for AsyncAPI document generation (server) and C# contract code generation (client). Provides the core types, services, and abstractions used by `ConcordIO.AsyncApi.Server` and `ConcordIO.AsyncApi.Client`.

This README is included as the NuGet package readme.

> **Note:** This library is not consumed directly by end users. It is referenced internally by the Server and Client MSBuild task packages.

## Features

> **Scope:** The generated AsyncAPI documents serve as a **contract catalog** — they describe message schemas and .NET type metadata, but do not include broker configuration (queue names, exchange bindings, server connections). Channel addresses use MassTransit URN format, not actual broker endpoints. Broker topology is determined at runtime by MassTransit.

- **Server-side**: Generate AsyncAPI 3.x documents from .NET types (MassTransit message contracts)
- **Client-side**: Generate C# contract types from AsyncAPI specifications
- **Type discovery**: Pattern-based discovery of event and command types from assemblies
- **External type resolution**: Detect types already defined in referenced assemblies to avoid duplicate generation
- **Cross-namespace support**: Preserve .NET namespaces via `x-dotnet-namespace` / `x-dotnet-type` extensions
- **Definition enum support**: Generates enum types declared inside schema `definitions` blocks (for example `#/definitions/MyEnum`)

## Key Types

### Shared Constants

| Type | Description |
|------|-------------|
| `AsyncApiConstants` | Static constants for AsyncAPI extension keys (`DotNetNamespace`, `DotNetType`). Used across Server and Client components. |

### Server (`ConcordIO.AsyncApi.Server` namespace)

| Type | Description |
|------|-------------|
| `AsyncApiDocumentGenerator` | Generates AsyncAPI 3.x documents from discovered .NET types. Uses NJsonSchema for JSON Schema generation. |
| `AsyncApiDocumentWriter` | Writes AsyncAPI documents to YAML or JSON files. |
| `TypeDiscoveryService` | Discovers types from assemblies using pattern matching (wildcards, interfaces, base classes). |
| `DiscoveredType` | Record representing a discovered type with its `MessageKind`. |
| `MessageTypePattern` | Record representing a type discovery pattern with its kind. |
| `MessageKind` | Enum: `Event` or `Command`. |

### Client (`ConcordIO.AsyncApi.Client` namespace)

| Type | Description |
|------|-------------|
| `AsyncApiContractGenerator` | Generates C# contract types from AsyncAPI documents using NJsonSchema code generation. |
| `ExternalTypeResolver` | Resolves types from external assemblies to skip generation of already-defined types. |
| `ContractGeneratorSettings` | Configurable settings for code generation (class style, annotations, type mappings). |
| `TypeInfo` | Record representing a type to be generated or referenced. |
| `GeneratedSourceFile` | Record representing a generated C# source file. |
| `ContractGenerationResult` | Result of the contract generation process. |
| `GeneratedClassStyle` | Enum: `Poco` or `Record`. |

## Dependencies

| Library | Purpose |
|---------|---------|
| `Neuroglia.AsyncApi.Core` | AsyncAPI 3.x document model |
| `NJsonSchema` | JSON Schema generation from .NET types |
| `NJsonSchema.CodeGeneration.CSharp` | C# code generation from JSON Schema |
| `Neuroglia.Serialization` | JSON/YAML serialization |

## Related Projects

- [ConcordIO.AsyncApi.Server](../ConcordIO.AsyncApi.Server/) — MSBuild task package for server-side AsyncAPI generation
- [ConcordIO.AsyncApi.Client](../ConcordIO.AsyncApi.Client/) — MSBuild task package for client-side C# generation
- [ConcordIO.Tool](../ConcordIO.Tool/) — CLI tool for contract package management

## License

Licensed under the MIT License.
