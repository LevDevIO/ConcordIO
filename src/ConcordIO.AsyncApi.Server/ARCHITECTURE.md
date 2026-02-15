# ConcordIO.AsyncApi.Server — Architecture

This document explains the internal design of the Server MSBuild task package. For usage instructions, see [README.md](README.md).

## High-Level Overview

ConcordIO.AsyncApi.Server is a NuGet tool package that runs after build in producer projects. It uses reflection to discover message types from the compiled assembly, generates an AsyncAPI 3.x document, and optionally packages it into the NuGet output.

```
┌──────────────────────────────────────────────────────────────┐
│  Producer Project Build                                      │
│                                                              │
│  Build (compile the assembly)                                │
│     ▼                                                        │
│  ConcordIOGenerateAsyncApi (AfterTargets=Build)              │
│     │                                                        │
│     ├─ Parse ConcordIOEventTypes / ConcordIOCommandTypes     │
│     │  into MessageTypePattern[] with Kind metadata          │
│     │                                                        │
│     ├─ GenerateAsyncApiTask.Execute()                        │
│     │  ├─ AssemblyLoadContext.LoadFromAssemblyPath()         │
│     │  ├─ TypeDiscoveryService.DiscoverTypes()               │
│     │  ├─ AsyncApiDocumentGenerator.Generate()               │
│     │  ├─ AsyncApiDocumentWriter.WriteYaml/JsonAsync()       │
│     │  └─ AssemblyLoadContext.Unload() (cleanup)             │
│     │                                                        │
│     └─ Output: _ConcordIOGeneratedFile                       │
│     ▼                                                        │
│  ConcordIOGenerateContractTargets                            │
│     │  Generates a .targets file for consumers               │
│     ▼                                                        │
│  ConcordIOIncludeAsyncApiInPackage (BeforeTargets=GenerateNuspec) │
│     │  Adds spec + targets to NuGet package                  │
└──────────────────────────────────────────────────────────────┘
```

## Target Framework Strategy

ConcordIO.AsyncApi.Server is multi-targeted to **.NET 9.0 and 10.0** only, due to NuGet dependency constraints (Neuroglia.AsyncApi.Core requires net9.0+).

**Impact on Consumers**:
- **Producer projects targeting net6.0, net7.0, or net8.0** can still generate AsyncAPI documents via custom tooling
- **But cannot use the MSBuild task at build time** if their project targets net6.0–8.0
- The `.targets` file dynamically resolves `$(TargetFramework)` to select the appropriate framework-specific task assembly

## Package Structure

```
ConcordIO.AsyncApi.Server.nupkg
├── build/
│   ├── ConcordIO.AsyncApi.Server.props    # Default MSBuild properties
│   └── ConcordIO.AsyncApi.Server.targets  # Task registration (uses dynamic $(TargetFramework) resolution)
├── buildTransitive/
│   └── ConcordIO.AsyncApi.Server.props    # Imports build/props for transitive consumers
└── tools/
    ├── net9.0/
    └── net10.0/
        ├── ConcordIO.AsyncApi.Server.dll  # MSBuild task assembly
        ├── ConcordIO.AsyncApi.dll         # Core library (PrivateAssets=all)
        └── (NJsonSchema, Neuroglia, etc.) # Dependencies bundled as tools
```

## Key Components

### GenerateAsyncApiTask (MSBuild Task)

Entry point invoked by MSBuild. Located in `Tasks/GenerateAsyncApiTask.cs`.

**Input properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `AssemblyPath` | Yes | Path to the compiled assembly (`$(TargetPath)`) |
| `MessageTypePatterns` | No | `ITaskItem[]` with `Kind` metadata (Event/Command) |
| `DocumentTitle` | No | AsyncAPI document title (defaults to assembly name) |
| `DocumentVersion` | No | AsyncAPI document version (defaults to `1.0.0`) |
| `OutputPath` | No | Output file path |
| `OutputFormat` | No | `"yaml"` or `"json"` |

**Output properties:**

| Property | Description |
|----------|-------------|
| `GeneratedFile` | Path to the generated AsyncAPI spec file |

### Document Generation Pipeline

```
MSBuild properties
    │
    ▼
ConcordIOGenerateAsyncApi target
    │  Parse semicolon-separated ConcordIOEventTypes/ConcordIOCommandTypes
    │  into <_ConcordIOAllMessageTypes> items with Kind metadata
    │
    ▼
GenerateAsyncApiTask.Execute()
    │
    ├─ Create collectible AssemblyLoadContext
    │  Prevents memory leaks in long-running MSBuild processes
    │
    ├─ AssemblyLoadContext.LoadFromAssemblyPath(AssemblyPath)
    │  Dependencies resolved within the collectible context
    │
    ├─ ParsePatterns()
    │  Convert ITaskItem[] → List<MessageTypePattern>
    │  Each item: pattern string + Kind (Event/Command)
    │
    ├─ TypeDiscoveryService.DiscoverTypes(assembly, patterns)
    │  │  Per pattern:
    │  │  ├─ *.** → recursive namespace wildcard
    │  │  ├─ .*  → exact namespace wildcard
    │  │  ├─ IsInterface → find all implementations
    │  │  ├─ IsAbstract/HasSubclasses → find all subclasses
    │  │  └─ Concrete → just that type
    │  └─ Returns: DiscoveredType[] (Type + MessageKind)
    │
    ├─ AsyncApiDocumentGenerator.Generate(title, version, types)
    │  │  1. CollectTypeAndDependencies() — walk properties recursively
    │  │  2. GenerateSchema() per type — NJsonSchema + x-dotnet-* extensions
    │  │  3. Build channels (MassTransit URN address)
    │  │  4. Build messages ($ref to schemas)
    │  │  5. Build operations (receive for events, send for commands)
    │  └─ Returns: V3AsyncApiDocument
    │
    └─ AsyncApiDocumentWriter.WriteYaml/JsonAsync(document, outputPath)
```

### MSBuild Integration

**Props** (`build/ConcordIO.AsyncApi.Server.props`):
- Sets defaults for `ConcordIOAsyncApiDocumentVersion` (falls back to `$(Version)` then `1.0.0`)
- Sets defaults for `ConcordIOAsyncApiOutputFormat` (`json`) and `ConcordIOIncludeAsyncApiInPackage` (`true`)
- Does NOT set `OutputPath` or `DocumentTitle` — these depend on `IntermediateOutputPath` and `AssemblyName` which aren't available at props evaluation time

**Targets** (`build/ConcordIO.AsyncApi.Server.targets`):

Three targets form the pipeline:

- The task registration uses explicit runtime/architecture hints (`CurrentRuntime` / `CurrentArchitecture`) to avoid MSBuild fallback task hosting

1. **`ConcordIOGenerateAsyncApi`** (`AfterTargets="Build"`)
   - Converts `ConcordIOEventTypes`/`ConcordIOCommandTypes` semicolon-separated properties into `_ConcordIOAllMessageTypes` items with `Kind` metadata
   - Computes output path at target time
   - Runs `GenerateAsyncApiTask`
   - Tracks generated file in `FileWrites` and `ConcordIOGeneratedAsyncApi` items

2. **`ConcordIOGenerateContractTargets`** (`AfterTargets="ConcordIOGenerateAsyncApi"`)
   - Auto-generates a consumer `.targets` file that exposes `ConcordIOAsyncApiContract` items
   - The `.targets` content uses `$(MSBuildThisFileDirectory)` for path resolution at consumer evaluation time
   - Other properties (`DocumentTitle`, `OutputExtension`, `Version`) are baked in at generation time

3. **`ConcordIOIncludeAsyncApiInPackage`** (`BeforeTargets="GenerateNuspec"`)
   - Includes the generated spec in the NuGet package under `asyncapi/`
   - Includes the auto-generated consumer `.targets` file under `build/{PackageId}.targets`

**Transitive** (`buildTransitive/ConcordIO.AsyncApi.Server.props`):

- Imports the main props file for projects that transitively reference this package

### Assembly Loading and Memory Management

The task uses a **collectible AssemblyLoadContext** to load the target assembly and its dependencies. This is critical for preventing memory leaks in long-running MSBuild processes (e.g., Visual Studio builds).

**Why collectible contexts?**

- `Assembly.LoadFrom()` loads assemblies into the default AppDomain where they cannot be unloaded
- In long-running processes, this causes memory to accumulate with each build
- Collectible `AssemblyLoadContext` allows the assemblies to be unloaded via `Unload()` after generation is complete

**Implementation:**

```csharp
var alc = new AssemblyLoadContext("ConcordIO-GenerateAsyncApi", isCollectible: true);
try
{
    var assembly = alc.LoadFromAssemblyPath(AssemblyPath);
    // ... generate document ...
}
finally
{
    alc.Unload();
}

```

Dependency assemblies are automatically resolved within the same context by the runtime, eliminating the need for custom `AssemblyResolve` handlers.

### Generated Consumer Targets

The auto-generated `.targets` file for consumers:

```xml
<Project>
  <ItemGroup>
    <ConcordIOAsyncApiContract Include="$(MSBuildThisFileDirectory)..\asyncapi\{Title}{Extension}">
      <PackageId>{PackageId}</PackageId>
      <Version>{Version}</Version>
    </ConcordIOAsyncApiContract>
  </ItemGroup>
</Project>
```

- `$(MSBuildThisFileDirectory)` resolves at consumer evaluation time (relative to the installed package)
- `{Title}`, `{Extension}`, `{PackageId}`, `{Version}` are baked in at generation time from the producer's build context
