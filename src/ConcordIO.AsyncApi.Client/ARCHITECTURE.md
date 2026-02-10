# ConcordIO.AsyncApi.Client — Architecture

This document explains the internal design of the Client MSBuild task package. For usage instructions, see [README.md](README.md).

## High-Level Overview

ConcordIO.AsyncApi.Client is a NuGet tool package that runs at build time in consuming projects. It reads AsyncAPI specification files (exposed as `ConcordIOAsyncApiContract` MSBuild items from contract packages) and generates C# source files that are compiled into the consumer's assembly.

```
┌─────────────────────────────────────────────────────────┐
│  Consumer Project Build                                 │
│                                                         │
│  ResolvePackageAssets                                    │
│     │  Contract package provides:                       │
│     │  <ConcordIOAsyncApiContract> items                 │
│     ▼                                                   │
│  ConcordIOGenerateContracts (BeforeTargets=CoreCompile) │
│     │                                                   │
│     ├─ Collect @(ReferencePath) for external types       │
│     │                                                   │
│     ├─ GenerateContractsTask.Execute()                  │
│     │  ├─ ExternalTypeResolver.LoadAssemblies()         │
│     │  ├─ For each AsyncAPI file:                       │
│     │  │  ├─ LoadAsyncApiDocument() (YAML/JSON)         │
│     │  │  ├─ AsyncApiContractGenerator.Generate()       │
│     │  │  └─ Write .g.cs files                          │
│     │  └─ Output: GeneratedFiles[]                      │
│     │                                                   │
│     └─ Add @(_ConcordIOClientGeneratedFiles) to         │
│        <Compile> and <FileWrites>                       │
│     ▼                                                   │
│  CoreCompile (includes generated .g.cs files)           │
└─────────────────────────────────────────────────────────┘
```

## Package Structure

```
ConcordIO.AsyncApi.Client.nupkg
├── build/
│   ├── ConcordIO.AsyncApi.Client.props    # Default MSBuild properties
│   └── ConcordIO.AsyncApi.Client.targets  # Task registration and build targets
├── buildTransitive/
│   └── ConcordIO.AsyncApi.Client.props    # Imports build/props for transitive consumers
└── tools/
    └── net10.0/
        ├── ConcordIO.AsyncApi.Client.dll  # MSBuild task assembly
        ├── ConcordIO.AsyncApi.dll         # Core library (PrivateAssets=all)
        └── (NJsonSchema, Neuroglia, etc.) # Dependencies bundled as tools
```

## Key Components

### GenerateContractsTask (MSBuild Task)

Entry point invoked by MSBuild. Located in `Tasks/GenerateContractsTask.cs`.

**Input properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `AsyncApiFiles` | Yes | `ITaskItem[]` — paths to AsyncAPI spec files |
| `OutputDirectory` | Yes | Output directory for generated `.g.cs` files |
| `ReferencedAssemblies` | No | `ITaskItem[]` — assembly paths for external type detection |
| `GenerateDataAnnotations` | No | `bool` — default `true` |
| `GenerateNullableReferenceTypes` | No | `bool` — default `true` |
| `ClassStyle` | No | `string` — `"Poco"` or `"Record"` |

**Output properties:**

| Property | Description |
|----------|-------------|
| `GeneratedFiles` | `ITaskItem[]` — paths to generated `.g.cs` files |

### Code Generation Pipeline

```
AsyncAPI file (YAML/JSON)
    │
    ▼
LoadAsyncApiDocument()
    │  YAML: Neuroglia YamlSerializer.Default.Deserialize<V3AsyncApiDocument>()
    │  JSON: System.Text.Json.JsonSerializer.Deserialize<V3AsyncApiDocument>()
    │
    ▼
AsyncApiContractGenerator.Generate()  [in ConcordIO.AsyncApi]
    │
    ├─ Collect schemas from document.Components.Schemas
    │  Extract x-dotnet-namespace per schema
    │
    ├─ Classify: external (skip) vs. generate
    │  ExternalTypeResolver checks @(ReferencePath) assemblies
    │
    ├─ Group by namespace
    │
    └─ Per namespace → GenerateNamespaceFile()
       │
       ├─ Build using statements
       │  - System, System.Collections.Generic
       │  - System.ComponentModel.DataAnnotations (if enabled)
       │  - Other namespaces in the document
       │  - External type namespaces
       │
       ├─ Per type → GenerateTypeFromSchema()
       │  ├─ ConvertToJsonSchema() — schema object → NJsonSchema.JsonSchema
       │  ├─ CSharpGenerator.GenerateFile() — NJsonSchema code generation
       │  └─ ExtractClassDefinition() — strip namespace/usings from output
       │
       └─ Output: GeneratedSourceFile (FileName, Namespace, Content, Types)
```

### MSBuild Integration

**Props** (`build/ConcordIO.AsyncApi.Client.props`):
- Sets defaults for `ConcordIOClientGenerateDataAnnotations`, `ConcordIOClientGenerateNullableReferenceTypes`, `ConcordIOClientClassStyle`
- Output path computed at target time (depends on `IntermediateOutputPath`)

**Targets** (`build/ConcordIO.AsyncApi.Client.targets`):
- Registers `GenerateContractsTask` via `<UsingTask>`
- `ConcordIOGenerateContracts` — runs before `CoreCompile`, generates code, adds to `<Compile>`
- `ConcordIOCleanGeneratedContracts` — cleans generated directory after `Clean`

**Transitive** (`buildTransitive/ConcordIO.AsyncApi.Client.props`):
- Imports the main props file for projects that transitively reference this package

### External Type Resolution

`ExternalTypeResolver` prevents duplicate type generation:

```
@(ReferencePath) assembly paths
    │
    ▼
LoadAssemblies()
    │  AssemblyLoadContext.LoadFromAssemblyPath() per path
    │  Uses collectible context to prevent memory leaks
    │  Cache GetExportedTypes() by FullName
    │  Skip assemblies that fail to load (native, etc.)
    │
    ▼
For each schema type:
    │  fullTypeName = "{x-dotnet-namespace}.{schemaName}"
    │  TypeExists(fullTypeName) → true: mark as external
    │                          → false: mark for generation
    │
    ▼
External types get using statements instead of generated code
    │
    ▼
Dispose() → AssemblyLoadContext.Unload()
```

**Memory Management:**
The resolver implements `IDisposable` and uses a collectible `AssemblyLoadContext`. The task disposes the resolver after generation, ensuring loaded assemblies are unloaded and don't accumulate in long-running MSBuild processes.

Dependencies are automatically resolved within the collectible `AssemblyLoadContext`, eliminating the need for custom `AppDomain.CurrentDomain.AssemblyResolve` handlers. The task assembly runs from the NuGet package's `tools/` folder, but the `AssemblyLoadContext` resolves consumer-referenced assemblies from the output directory.
