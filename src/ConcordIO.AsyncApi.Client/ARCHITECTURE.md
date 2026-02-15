# ConcordIO.AsyncApi.Client — Architecture

This document explains the internal design of the Client MSBuild task package. For usage instructions, see [README.md](README.md).

## High-Level Overview

ConcordIO.AsyncApi.Client is a NuGet tool package that runs at build time in consuming projects. It reads AsyncAPI specification files (exposed as `ConcordIOAsyncApiContract` MSBuild items from contract packages) and generates C# source files that are compiled into the consumer's assembly.

```text
┌─────────────────────────────────────────────────────────┐
│  Consumer Project Build                                 │
│                                                         │
│  ResolvePackageAssets                                    │
│     │  Contract package provides:                       │
│     │  <ConcordIOAsyncApiContract> items                 │
│     ▼                                                   │
│  ResolveAssemblyReferences                               │
│     │  Populates @(ReferencePath)                       │
│     ▼                                                   │
│  ConcordIOGenerateContracts (BeforeTargets=CoreCompile) │
│     │  (DependsOnTargets=ResolveAssemblyReferences)     │
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

## Target Framework Strategy

ConcordIO.AsyncApi.Client is multi-targeted to **.NET 9.0 and 10.0** to match the AsyncAPI dependency baseline.

**Impact on Consumers**:

- **Consuming projects targeting net9.0+** can use the MSBuild task at build time regardless of their TFM, because the task assembly is selected by the **MSBuild runtime**.
- **The `.targets` file selects the task TFM from the MSBuild runtime version**, not from `$(TargetFramework)`, to avoid MetadataLoadContext-only loading when MSBuild runs on a different runtime than the project being built.

## Package Structure

```text
ConcordIO.AsyncApi.Client.nupkg
├── build/
│   ├── ConcordIO.AsyncApi.Client.props    # Default MSBuild properties
│   └── ConcordIO.AsyncApi.Client.targets  # Task registration (selects task by MSBuild runtime)
├── buildTransitive/
│   └── ConcordIO.AsyncApi.Client.props    # Imports build/props for transitive consumers
└── tools/
    ├── net9.0/
    └── net10.0/
        ├── ConcordIO.AsyncApi.Client.dll  # MSBuild task assembly
        ├── ConcordIO.AsyncApi.dll         # Core library (PrivateAssets=all)
        └── (NJsonSchema, Neuroglia, etc.) # Dependencies bundled as tools
```text

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

```text
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
- Resolves the task assembly from the package `tools/$(_ConcordIOClientTaskTfm)` folder, where the task TFM is selected based on the MSBuild runtime version
- Uses explicit task runtime/architecture hints (`CurrentRuntime` / `CurrentArchitecture`) to avoid MSBuild fallback task hosting
- `ConcordIOGenerateContracts` — depends on `ResolveAssemblyReferences` to ensure `@(ReferencePath)` is populated, runs before `CoreCompile`, generates code, adds to `<Compile>`
- `ConcordIOCleanGeneratedContracts` — cleans generated directory after `Clean`

**Transitive** (`buildTransitive/ConcordIO.AsyncApi.Client.props`):

- Imports the main props file for projects that transitively reference this package

### External Type Resolution

`ExternalTypeResolver` prevents duplicate type generation:

```text
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
