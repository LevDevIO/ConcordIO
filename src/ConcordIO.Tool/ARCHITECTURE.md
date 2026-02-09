# ConcordIO.Tool — Architecture

This document explains the internal design and code structure of the ConcordIO CLI tool. For usage instructions, see [README.md](README.md).

## High-Level Overview

ConcordIO.Tool is a .NET CLI tool (distributed as a `dotnet tool`) that generates NuGet package scaffolds from API specification files. The generated packages use MSBuild integration (`.targets` files) so that consuming projects get contracts and auto-generated clients at build time — without copying spec files.

```
┌─────────────────────────────────────────────────────────────────┐
│                         CLI (DotMake)                           │
│  ┌──────────────┐  ┌────────────────┐  ┌─────────────────────┐  │
│  │GenerateCommand│  │BreakingCommand │  │  GetSpecCommand    │  │
│  └──────┬───────┘  └───────┬────────┘  └──────────┬──────────┘  │
│         │                  │                      │             │
│  ┌──────▼───────┐  ┌───────▼────────┐  ┌──────────▼──────────┐  │
│  │ContractPkg   │  │  OasDiffRunner │  │   NuGetService      │  │
│  │Generator     │  │  (oasdiff bin) │  │   (nuget CLI)       │  │
│  └──────┬───────┘  └────────────────┘  └─────────────────────┘  │
│         │                                                       │
│  ┌──────▼───────┐                                               │
│  │ Template     │                                               │
│  │ Renderer     │                                               │
│  │ (Scriban)    │                                               │
│  └──────┬───────┘                                               │
│         │                                                       │
│  ┌──────▼────────────────────────────────────────────────────┐  │
│  │ Embedded Templates (.nuspec / .targets)                   │  │
│  │  Contract/          Contract.Client/                      │  │
│  │  Contract.AsyncApi/ Contract.AsyncApi.Client/             │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## Project Structure

```
ConcordIO.Tool/
├── Program.cs                   # Entry point — routes to DotMake CLI
├── ConcordIO.Tool.csproj        # Tool packaging, oasdiff bundling
│
├── CliCommands/                 # CLI command definitions (DotMake)
│   ├── GenerateCommand.cs       # `concordio generate` — package scaffold generation
│   ├── BreakingCommand.cs       # `concordio breaking` — breaking-change detection
│   └── GetSpecCommand.cs        # `concordio get-spec` — spec retrieval from NuGet
│
├── Services/                    # Core business logic and abstractions
│   ├── SpecKind.cs              # Constants for specification kinds (openapi, proto, asyncapi)
│   ├── ContractPackageGenerator.cs  # Orchestrates template rendering → file output
│   ├── TemplateRenderer.cs      # Scriban template engine (reads embedded resources)
│   ├── ITemplateRenderer.cs     # Interface for template rendering
│   ├── FileSystem.cs            # System.IO wrapper
│   ├── IFileSystem.cs           # Interface for file operations
│   ├── NuGetService.cs          # Shells out to `nuget` CLI
│   ├── INuGetService.cs         # Interface for NuGet operations
│   ├── IOasDiffRunner.cs        # Interface for OpenAPI diff operations
│   └── StringHelpers.cs         # Shared utilities (class name sanitization, key=value parsing)
│
├── AOComparison/                # OpenAPI comparison subsystem
│   ├── OasDiffRunner.cs         # Wraps bundled oasdiff binary, implements IOasDiffRunner
│   └── oasdiff_bin/             # Platform-specific oasdiff binaries (win/linux/mac)
│
└── Templates/                   # Scriban templates (embedded resources)
    ├── Contract/                # Contract package templates
    │   ├── Contract.nuspec      # NuSpec for the contract package
    │   └── Contracts.targets    # MSBuild targets exposing ConcordIOContract items
    ├── Contract.Client/         # Client package templates
    │   ├── Contract.Client.nuspec   # NuSpec for the client (dev dependency)
    │   └── Contract.Client.targets  # MSBuild targets wiring specs to code generators
    ├── Contract.AsyncApi/       # (placeholder for AsyncAPI contract templates)
    └── Contract.AsyncApi.Client/# (placeholder for AsyncAPI client templates)
```

## Key Components

### CLI Layer — DotMake.CommandLine

The tool uses [DotMake.CommandLine](https://github.com/dotmake-build/command-line) for CLI parsing. All commands are nested classes inside a partial `RootCommand`:

- **`RootCommand`** — top-level command definition (partial class spanning all command files).
- **`GenerateCommand`** — parses `--spec path[:kind]` arguments, groups specs by kind, then delegates to `ContractPackageGenerator`.
- **`BreakingCommand`** — downloads the published NuGet package via `GetSpecCommand`, extracts the spec, then runs `OasDiffRunner` to compare.
- **`GetSpecCommand`** — downloads a NuGet package using the `nuget` CLI and extracts the spec file from it.

Each command's `RunAsync()` method returns an `int` exit code (0 = success).

### Template Rendering Pipeline

This is the core of the `generate` command:

```
CLI options
    │
    ▼
GenerateCommand.RunAsync()
    │  Parses --spec args into SpecEntry(FileName, Kind)
    │  Groups by kind, builds ContractPackageOptions / ClientPackageOptions
    │
    ▼
ContractPackageGenerator
    │  Builds a Dictionary<string, object> model from options
    │  Calls TemplateRenderer.RenderAsync() for each template
    │  Writes rendered output via IFileSystem
    │
    ▼
TemplateRenderer (Scriban)
    │  Loads template from assembly embedded resources
    │  Resource name: "ConcordIO.Tool.Templates.{templateName}"
    │  e.g. "Contract.Contract.nuspec" → embedded resource "ConcordIO.Tool.Templates.Contract.Contract.nuspec"
    │
    ▼
Output files (.nuspec + build/{PackageId}.targets)
```

Templates are Scriban `.nuspec` and `.targets` files embedded as assembly resources (configured in the `.csproj` via `<EmbeddedResource Include="Templates\**\*" />`). The `TemplateRenderer` loads them by convention: the template name maps to the resource name with a `ConcordIO.Tool.Templates.` prefix, using dots as folder separators.

### Template Model

The model passed to Scriban templates is a `Dictionary<string, object>`. Key fields:

**Contract package model:**
| Key | Type | Description |
|-----|------|-------------|
| `package_id` | `string` | NuGet package ID |
| `version` | `string` | SemVer version |
| `authors` | `string` | Package authors |
| `description` | `string` | Package description |
| `package_properties` | `KeyValuePair<string,string>[]` | Extra NuSpec metadata elements |
| `specs_by_kind` | `Dictionary<string, List<string>>` | Spec filenames grouped by kind |
| `has_openapi` | `bool` | Whether the package contains OpenAPI specs |
| `has_proto` | `bool` | Whether the package contains Proto specs |
| `has_asyncapi` | `bool` | Whether the package contains AsyncAPI specs |

**Client package model** adds:
| Key | Type | Description |
|-----|------|-------------|
| `client_package_id` | `string` | Client package ID |
| `contract_package_id` | `string` | The contract package this client depends on |
| `contract_version` | `string` | Version of the contract dependency |
| `nswag_client_class_name` | `string` | Generated C# client class name |
| `nswag_output_path` | `string` | Output file path for NSwag |
| `nswag_options` | `List<KeyValuePair<string,string>>` | Additional NSwag MSBuild properties |
| `client_options` | `List<KeyValuePair<string,string>>` | Additional AsyncAPI client properties |

### Generated NuGet Package Structure

#### Contract Package

The contract package bundles spec files and exposes them via MSBuild:

```
{PackageId}.nupkg
├── {PackageId}.nuspec
├── contentFiles/any/any/         # Specs as content files
│   └── petstore.yaml
├── openapi/                      # Specs organized by kind
│   └── petstore.yaml
└── build/
    └── {PackageId}.targets       # Exposes ConcordIOContract MSBuild items
```

The `.targets` file defines `<ConcordIOContract>` items (for OpenAPI/Proto) or `<ConcordIOAsyncApiContract>` items (for AsyncAPI) that point to the spec files inside the package. Consuming projects see these items automatically after package restore.

#### Client Package

The client package is a development dependency that wires contracts to code generators:

```
{PackageId}.Client.nupkg
├── {PackageId}.Client.nuspec     # developmentDependency=true
└── build/
    └── {PackageId}.Client.targets
```

The client `.targets` file:
- **OpenAPI**: Creates `<OpenApiReference>` items pointing to the contract's `ConcordIOContract` items, configured for NSwag C# code generation. Runs `AfterTargets="ResolvePackageAssets"` to pick up contracts from restored packages.
- **AsyncAPI**: Adds metadata to `<ConcordIOAsyncApiContract>` items for `ConcordIO.AsyncApi.Client` to process.

The client NuSpec declares transitive dependencies on the contract package and the appropriate code generator (`NSwag.ApiDescription.Client` for OpenAPI, `ConcordIO.AsyncApi.Client` for AsyncAPI).

### Breaking-Change Detection

The `breaking` command compares a local spec against a published one:

```
concordio breaking --spec local.yaml --package-id Contoso.Api
    │
    ▼
1. Create temp directory
2. GetSpecCommand.RunAsync()
    │  Shells out to `nuget install {PackageId} -OutputDirectory {tempDir}`
    │  Finds the spec file inside the downloaded package's openapi/ folder
    │  Copies it to a known path
    │
    ▼
3. OasDiffRunner.Breaking(localSpec, nugetSpec, args)
    │  Resolves platform-specific oasdiff binary from bundled binaries
    │  Runs: oasdiff breaking "{base}" "{revision}" -o WARN {extraArgs}
    │
    ▼
4. Returns OasDiffResult { ExitCode, Output, Error, Breaking }
5. Cleanup temp directory
```

### oasdiff Binary Bundling

Platform-specific [oasdiff](https://github.com/Tufin/oasdiff) binaries are bundled in `AOComparison/oasdiff_bin/` and included as `<Content>` items in the `.csproj` with `CopyToOutputDirectory=PreserveNewest`. At runtime, `OasDiffRunner.GetOasDiffPath()` selects the correct binary based on `RuntimeInformation.IsOSPlatform()` and `RuntimeInformation.ProcessArchitecture`:

| Platform | Path |
|----------|------|
| Windows x64 | `oasdiff/win-x64/oasdiff.exe` |
| Windows ARM64 | `oasdiff/win-arm64/oasdiff.exe` |
| Linux x64 | `oasdiff/linux-x64/oasdiff` |
| Linux ARM64 | `oasdiff/linux-arm64/oasdiff` |
| macOS (universal) | `oasdiff/osx/oasdiff` |

### Service Abstractions

The codebase defines interfaces for testability:

| Interface | Implementation | Purpose |
|-----------|---------------|---------|
| `IFileSystem` | `FileSystem` | Wraps `System.IO` for directory/file operations |
| `ITemplateRenderer` | `TemplateRenderer` | Renders Scriban templates from embedded resources |
| `INuGetService` | `NuGetService` | Shells out to `nuget` CLI to download packages |
| `IOasDiffRunner` | `OasDiffRunner` | Wraps the bundled oasdiff binary |

### Spec Kind System

The tool supports three specification kinds via the `SpecKind` class (in `Services/SpecKind.cs`):

| Constant | Value | Description | Code generator (client) |
|----------|-------|-------------|------------------------|
| `SpecKind.OpenApi` | `"openapi"` | OpenAPI JSON/YAML specs | NSwag (via `NSwag.ApiDescription.Client`) |
| `SpecKind.Proto` | `"proto"` | Protocol Buffer `.proto` files | (not yet implemented for client gen) |
| `SpecKind.AsyncApi` | `"asyncapi"` | AsyncAPI YAML/JSON specs | `ConcordIO.AsyncApi.Client` |
| `SpecKind.All` | `["openapi", "proto", "asyncapi"]` | Array of all supported kinds | — |

The `SpecKind` class centralizes these constants, replacing magic string literals throughout the codebase. `GenerateCommand` and `ContractPackageGenerator` use these constants when validating spec kinds and building template models.

When using `--spec`, the kind can be specified as a suffix: `--spec myfile.yaml:asyncapi`. Without a suffix, it defaults to `SpecKind.OpenApi`.

### NSwag Default Options

When generating OpenAPI client packages, `GenerateCommand` injects these NSwag defaults unless overridden:

| Property | Default | Purpose |
|----------|---------|---------|
| `NSwagJsonLibrary` | `SystemTextJson` | Use System.Text.Json instead of Newtonsoft |
| `NSwagJsonPolymorphicSerializationStyle` | `SystemTextJson` | Polymorphic serialization via STJ |
| `NSwagGenerateExceptionClasses` | `true` | Generate typed exception classes |

Users can override or extend these via `--nswag-options`.

## Data Flow Summary

```
User runs: concordio generate --spec api.yaml --package-id Foo --version 1.0.0

1. DotMake parses CLI args → GenerateCommand properties
2. ParseSpecEntries: "api.yaml" → [SpecEntry("api.yaml", "openapi")]
3. Group by kind: { "openapi": ["api.yaml"] }
4. ContractPackageGenerator.GenerateContractPackageAsync():
   a. Build model dict with package_id, version, specs_by_kind, has_openapi=true, etc.
   b. TemplateRenderer renders "Contract.Contract.nuspec" → Foo.nuspec
   c. TemplateRenderer renders "Contract.Contracts.targets" → build/Foo.targets
   d. FileSystem writes both files to output directory
5. ContractPackageGenerator.GenerateClientPackageAsync():
   a. Build client model with client_package_id="Foo.Client", nswag_client_class_name, etc.
   b. TemplateRenderer renders "Contract.Client.Contract.Client.nuspec" → Foo.Client.nuspec
   c. TemplateRenderer renders "Contract.Client.Contract.Client.targets" → build/Foo.Client.targets
   d. FileSystem writes both files to output directory
6. Output: Foo.nuspec, build/Foo.targets, Foo.Client.nuspec, build/Foo.Client.targets
```

The output directory is then ready for `nuget pack` to produce `.nupkg` files.
