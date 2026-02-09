# Copilot Instructions for ConcordIO

## Project Overview

ConcordIO is a .NET CLI tool and NuGet-based contract management toolchain. It generates NuGet package scaffolds from OpenAPI, Protocol Buffer, and AsyncAPI specifications — with automatic MSBuild integration for client code generation and breaking-change detection.

## Key Documentation — Keep Updated

When making changes to the codebase, **always update the relevant documentation files** listed below. These files must stay in sync with the code at all times.

### `src/ConcordIO.Tool/README.md`
- **Purpose**: User-facing documentation — installation, CLI commands, options, examples.
- **Update when**: Adding/removing/renaming CLI commands or options, changing default values, changing exit codes, changing supported spec kinds, modifying prerequisites or platform support.

### `src/ConcordIO.Tool/ARCHITECTURE.md`
- **Purpose**: Internal architecture documentation — code structure, component design, data flow, template model, generated package layout.
- **Update when**: Adding/removing/renaming services or interfaces, changing the template rendering pipeline, modifying the template model schema, changing the generated package structure, adding new spec kinds, modifying the oasdiff integration, restructuring folders or namespaces.

### `src/ConcordIO.AsyncApi/README.md`
- **Purpose**: Library overview — key types, dependencies, feature summary.
- **Update when**: Adding/removing/renaming public types or namespaces, changing dependencies, adding new features to the shared library.

### `src/ConcordIO.AsyncApi/ARCHITECTURE.md`
- **Purpose**: Internal architecture — component design, data flow, namespace handling, server/client pipelines.
- **Update when**: Adding/removing/renaming services or classes, changing the schema generation pipeline, modifying the code generation pipeline, changing how namespaces or extensions are handled, restructuring the Server/ or Client/ folders.

### `src/ConcordIO.AsyncApi.Client/README.md`
- **Purpose**: User-facing documentation — installation, MSBuild properties, generated output format, external type detection.
- **Update when**: Adding/removing/renaming MSBuild properties, changing defaults, changing the generated code format, modifying the external type resolution behavior, changing MSBuild target names or ordering.

### `src/ConcordIO.AsyncApi.Client/ARCHITECTURE.md`
- **Purpose**: Internal architecture — MSBuild task design, code generation pipeline, package structure, assembly resolution.
- **Update when**: Changing the GenerateContractsTask, modifying the code generation flow, changing how external types are resolved, modifying the MSBuild targets/props files, changing the NuGet package layout.

### `src/ConcordIO.AsyncApi.Server/README.md`
- **Purpose**: User-facing documentation — installation, type discovery patterns, MSBuild properties, generated AsyncAPI format, NuGet packaging.
- **Update when**: Adding/removing/renaming MSBuild properties, changing defaults, changing the generated AsyncAPI document structure, modifying type discovery patterns, changing MSBuild target names or ordering.

### `src/ConcordIO.AsyncApi.Server/ARCHITECTURE.md`
- **Purpose**: Internal architecture — MSBuild task design, document generation pipeline, type discovery, package structure, consumer targets generation.
- **Update when**: Changing the GenerateAsyncApiTask, modifying type discovery logic, changing the document generation flow, modifying the MSBuild targets/props files, changing the NuGet package layout, changing the auto-generated consumer targets.

### `README.md` (repo root)
- **Purpose**: High-level project overview and vision.
- **Update when**: The project scope, supported spec types, or major capabilities change.

## Project Structure

- `src/ConcordIO.Tool/` — The CLI tool (entry point, commands, services, templates).
  - `CliCommands/` — All CLI commands as nested classes of partial `RootCommand` (GenerateCommand, BreakingCommand, GetSpecCommand).
  - `Services/` — Business logic abstractions (`IFileSystem`, `ITemplateRenderer`, `INuGetService`, `IOasDiffRunner`).
  - `Templates/` — Scriban templates (`.nuspec`, `.targets`) organized by spec kind (Contract/, Contract.Client/, Contract.AsyncApi/).
  - `AOComparison/` — OpenAPI diff subsystem (`OasDiffRunner`, bundled oasdiff binaries).
- `src/ConcordIO.AsyncApi/` — AsyncAPI document parsing and code generation library.
  - `Server/` — Type discovery and AsyncAPI document generation (server-side).
  - `Client/` — C# code generation from AsyncAPI schemas (client-side).
- `src/ConcordIO.AsyncApi.Client/` — MSBuild task package for AsyncAPI client generation at build time.
  - `Tasks/` — `GenerateContractsTask` MSBuild task.
  - `build/` — MSBuild `.props` and `.targets` files.
- `src/ConcordIO.AsyncApi.Server/` — MSBuild task package for AsyncAPI server-side document generation.
- `src/ConcordIO.AsyncApi.Tests/` — Tests for the AsyncAPI libraries (unit, integration, E2E).
- `src/ConcordIO.Tool.Tests/` — Tests for the CLI tool (unit, integration, E2E).

## Tech Stack

- **.NET 10** with C# latest
- **DotMake.CommandLine** for CLI parsing (commands are nested classes inside a partial `RootCommand`)
- **Scriban** for template rendering (templates are embedded assembly resources)
- **oasdiff** (bundled native binaries) for OpenAPI breaking-change detection
- **NuGet CLI** (external dependency) for package download in `breaking` / `get-spec` commands
- **NJsonSchema** for JSON Schema generation (server) and C# code generation (client)
- **Neuroglia.AsyncApi** for AsyncAPI 3.x document model and serialization
- **xUnit** + **Verify** for testing (snapshot tests use centralized `Snapshots/` directory)

## Conventions

### CLI & Commands
- CLI commands live in `CliCommands/` as **nested classes of partial `RootCommand`** (DotMake.CommandLine pattern).
- Each command file declares `public partial class RootCommand` and defines a nested `[CliCommand]` class.
- Entry point: `Program.cs` calls `Cli.RunAsync<RootCommand>(args)`.
- Commands return `int` exit codes (0 = success).

### Services & Abstractions
- Service interfaces live in `Services/` (e.g., `IFileSystem`, `ITemplateRenderer`, `INuGetService`, `IOasDiffRunner`).
- **Always define interfaces for testability** — unit tests mock these interfaces.
- Example: `TemplateRenderer` implements `ITemplateRenderer` for Scriban template rendering.

### Template Rendering
- Templates are **Scriban files** (`.nuspec`, `.targets`) in `Templates/` subdirectories.
- Templates are **embedded as assembly resources** via `<EmbeddedResource Include="Templates\**\*" />` in `.csproj`.
- Resource naming: `ConcordIO.Tool.Templates.{Folder}.{FileName}` (dots as separators, e.g., `Contract.Contract.nuspec`).
- `TemplateRenderer.RenderAsync(templateName, model)` loads by convention: `templateName` → `ConcordIO.Tool.Templates.{templateName}`.
- Template model is `Dictionary<string, object>` with keys like `package_id`, `version`, `specs_by_kind`, `has_openapi`, etc.

### Spec Kinds & Constants
- Spec kinds are string constants: `"openapi"`, `"proto"`, `"asyncapi"`.
- Spec parsing: `--spec path[:kind]` where kind defaults to `"openapi"`.
- Specs are grouped by kind in commands: `Dictionary<string, List<string>>`.

### AsyncAPI Extensions
- Extension keys: `x-dotnet-namespace`, `x-dotnet-type`.
- Used for namespace/type mapping in both server (document generation) and client (code generation).

### MSBuild Integration
- MSBuild task packages use:
  - `build/` for `.props`/`.targets` (direct consumers).
  - `buildTransitive/` for transitive `.props` imports.
  - `tools/` for task assemblies and bundled dependencies.
- Contract packages expose specs as MSBuild items: `<ConcordIOContract>` (OpenAPI/Proto) or `<ConcordIOAsyncApiContract>` (AsyncAPI).
- Client packages wire contracts to code generators (e.g., NSwag for OpenAPI, ConcordIO.AsyncApi.Client for AsyncAPI).

## Testing

### General Testing
- Run all tests: `dotnet test src/ConcordIO.Tool.sln`
- Test framework: **xUnit** + **Verify** (snapshot testing).
- Snapshot tests use centralized `Snapshots/` directory (configured in `ModuleInitializer.cs` via `Verifier.UseProjectRelativeDirectory("Snapshots")`).

### Test Organization
- **Unit tests**: Mock interfaces (`IFileSystem`, `ITemplateRenderer`, etc.) — fast, isolated.
- **Integration tests**: Test service interactions (e.g., `OasDiffRunner` with real binaries, `ContractPackageGenerator` with real templates).
- **E2E tests**: Full CLI execution + real NuGet packages in isolated test contexts.

### E2E Testing Pattern
- E2E tests use `IntegrationTestFixture` and `TestContext` for isolated environments.
- Each test gets a unique temporary directory to avoid conflicts.
- Tests invoke the CLI via `dotnet run --project ConcordIO.Tool.csproj -- <command>` to test the real user experience.
- Example pattern: Generate package → Pack as NuGet → Reference in test project → Build test project → Verify MSBuild items.
- Collection-based fixtures: `[Collection(IntegrationTestCollection.Name)]` for shared fixture lifetime.

### Snapshot Testing with Verify
- Snapshot tests use `await Verify(output)` to compare against approved snapshots.
- Verify configuration in `ModuleInitializer.cs`: `VerifyDiffPlex.Initialize()` + `Verifier.UseProjectRelativeDirectory("Snapshots")`.
- Snapshots are committed to the repo (under `Snapshots/` directory).

## Build & Run

- Build: `dotnet build src/ConcordIO.Tool.sln`
- Run the tool locally: `dotnet run --project src/ConcordIO.Tool -- <command> [options]`
  - Example: `dotnet run --project src/ConcordIO.Tool -- generate --spec petstore.yaml --package-id Test.Api --version 1.0.0`
- Pack as tool: `dotnet pack src/ConcordIO.Tool/ConcordIO.Tool.csproj`
- Pack AsyncAPI packages: `dotnet pack src/ConcordIO.AsyncApi.Client/ConcordIO.AsyncApi.Client.csproj` and `dotnet pack src/ConcordIO.AsyncApi.Server/ConcordIO.AsyncApi.Server.csproj`
- VS Code tasks: Available tasks are `build`, `test`, `watch`, `clean`, `publish` (see `.vscode/tasks.json`).

## Breaking-Change Detection

- `breaking` command uses **oasdiff** (bundled as platform-specific binaries in `AOComparison/oasdiff_bin/`).
- Workflow: Download published NuGet package → Extract spec → Run `oasdiff breaking` → Return exit code + output.
- `OasDiffRunner` resolves platform-specific binary and shells out to `oasdiff breaking "{base}" "{revision}" -o WARN {extraArgs}`.
- Exit code 0 = no breaking changes; non-zero = breaking changes detected.

## Generated Package Structure

### Contract Package
- Contains spec files in `openapi/`, `proto/`, or `asyncapi/` folders.
- Includes `contentFiles/any/any/` for IDE support (spec files as content).
- Exposes specs via `.targets` file as `<ConcordIOContract>` or `<ConcordIOAsyncApiContract>` MSBuild items.

### Client Package
- Development dependency (`developmentDependency=true` in `.nuspec`).
- Contains `.targets` file that wires contract items to code generators.
- For OpenAPI: Creates `<OpenApiReference>` items for NSwag.
- For AsyncAPI: Adds metadata to `<ConcordIOAsyncApiContract>` for `ConcordIO.AsyncApi.Client` task.
- Declares transitive dependencies on contract package and code generator packages.
