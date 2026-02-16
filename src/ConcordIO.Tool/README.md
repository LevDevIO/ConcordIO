# ConcordIO.Tool

A .NET CLI tool for managing API contract packages. Generate, publish, and govern **OpenAPI**, **Protocol Buffers**, and **AsyncAPI** contracts as NuGet packages — with automatic client generation and breaking-change detection.

## Installation

```bash
dotnet tool install --global ConcordIO.Tool
```

Or as a local tool:

```bash
dotnet new tool-manifest
dotnet tool install ConcordIO.Tool
```

Once installed, the tool is available as `concordio`.

## Commands

### `concordio generate`

Generate contract and client NuGet packages from specification files.

```bash
concordio generate \
  --spec <path[:kind]> \
  --package-id <id> \
  --version <semver>
```

**Required options:**

| Option | Description |
|--------|-------------|
| `--spec` | Specification file(s) with optional kind suffix (format: `path[:kind]`). Kind defaults to `openapi`. Valid kinds: `openapi`, `proto`, `asyncapi`. Can be specified multiple times. |
| `--package-id` | Package ID for the generated NuGet package. |
| `--version` | SemVer version for the package. |

**Optional options:**

| Option | Default | Description |
|--------|---------|-------------|
| `--authors` | `ConcordIO` | Package authors. |
| `--description` | Auto-generated | Package description. |
| `--output` | `.` | Output directory for generated files. |
| `--client` | `true` | Also generate a client package. |
| `--client-package-id` | `{PackageId}.Client` | Client package ID. |
| `--client-class-name` | Derived from PackageId | Client class name (OpenAPI only). |
| `--nswag-options` | — | Additional NSwag options as `key=value` (OpenAPI only, repeatable). Values may contain '=' characters. |
| `--client-options` | — | Additional client options as `key=value` (AsyncAPI only, repeatable). Values may contain '=' characters. |
| `--package-properties` | — | Additional NuSpec metadata as `key=value` (repeatable). Values may contain '=' characters. |

For OpenAPI client generation, ConcordIO applies these NSwag defaults unless you override them via `--nswag-options`:

- `NSwagJsonLibrary=SystemTextJson`
- `NSwagJsonPolymorphicSerializationStyle=SystemTextJson`
- `NSwagGenerateNullableReferenceTypes=false`
- `NSwagGenerateExceptionClasses=true`

### Customizing Generated Clients

Client packages generated from OpenAPI contracts wire specs to NSwag at build time. You can customize the generated client by overriding `OpenApiReference` metadata in your consuming project.

#### Standard Metadata (No Prefix)

These properties control MSBuild orchestration and don't require the `NSwag` prefix:

- **`Namespace`** — Generated code namespace
- **`ClassName`** — Generated client class name
- **`OutputPath`** — Location of generated file
- **`CodeGenerator`** — Code generator to use (e.g., `NSwagCSharp`)

#### NSwag Generator Options (Prefix Required)

NSwag-specific code generation settings must be prefixed with `NSwag`:

- **`NSwagJsonLibrary`** — JSON serializer (`SystemTextJson` or `NewtonsoftJson`)
- **`NSwagGenerateNullableReferenceTypes`** — Enable nullable reference types (`true` or `false`)
- **`NSwagGenerateExceptionClasses`** — Generate typed exception classes (`true` or `false`)
- **`NSwagInjectHttpClient`** — Use dependency injection for HttpClient (`true` or `false`)
- **`NSwagGenerateClientInterfaces`** — Generate client interfaces (`true` or `false`)

#### Example: Override Namespace and Disable Nullable Reference Types

```xml
<Target Name="CustomizeOpenApiReference" AfterTargets="ConcordIOAddOpenApiReferenceForNSwag">
  <ItemGroup>
    <OpenApiReference Update="@(OpenApiReference)">
      <Namespace>MyApp.Generated.Clients</Namespace>
      <NSwagGenerateNullableReferenceTypes>false</NSwagGenerateNullableReferenceTypes>
    </OpenApiReference>
  </ItemGroup>
</Target>
```

**Why the difference?** The unprefixed properties (`Namespace`, `ClassName`, etc.) are defined by `Microsoft.Extensions.ApiDescription.Client` — the MSBuild infrastructure that NSwag builds upon. The `NSwag`-prefixed properties are passed directly to NSwag's code generator.

**Examples:**

```bash
# OpenAPI contract + client package
concordio generate \
  --spec petstore.yaml \
  --package-id Contoso.PetStore.Api \
  --version 1.0.0

# Multiple specs with explicit kinds
concordio generate \
  --spec api.yaml:openapi \
  --spec events.yaml:asyncapi \
  --package-id Contoso.PetStore.Api \
  --version 2.0.0

# Proto contract, no client package
concordio generate \
  --spec service.proto:proto \
  --package-id Contoso.Grpc.Api \
  --version 1.0.0 \
  --client false

# With custom NSwag options
concordio generate \
  --spec api.json \
  --package-id Contoso.Api \
  --version 1.0.0 \
  --nswag-options GenerateClientInterfaces=true \
  --nswag-options InjectHttpClient=true
```

**Output:**

The `generate` command produces two NuGet package scaffolds:

1. **Contract package** (`{PackageId}`) — contains the specification files and `.targets` files in both `build/` and `buildTransitive/` so `ConcordIOContract` items flow to direct and transitive consumers (for example, when only a client package is referenced).
2. **Client package** (`{PackageId}.Client`) — contains a `.targets` file that wires contract specs to code generators (NSwag for OpenAPI, ConcordIO.AsyncApi.Client for AsyncAPI) at build time. For AsyncAPI, the generated client package depends on `ConcordIO.AsyncApi.Client` with a minimum version equal to the current `ConcordIO.Tool` version (NuGet range `[toolVersion,)`).

---

### `concordio breaking`

Compare a local specification against the latest published version in a NuGet package and report breaking changes. Uses [oasdiff](https://github.com/Tufin/oasdiff) under the hood.

```bash
concordio breaking \
  --spec <path> \
  --package-id <id>
```

**Required options:**

| Option | Description |
|--------|-------------|
| `--spec` | Path to the local specification file. |
| `--package-id` | Package ID of the published contract NuGet package to compare against. |

**Optional options:**

| Option | Default | Description |
|--------|---------|-------------|
| `--version` | Latest | Version of the NuGet package to compare against. |
| `--prerelease` | `false` | Include prerelease versions when resolving the package. |
| `--kind` | `openapi` | Contract kind: `openapi` or `proto`. |
| `--working-directory` | Temp directory | Working directory for downloading the package. |
| `--cli-options` | — | Additional oasdiff CLI options as `key=value` (repeatable). |

**Examples:**

```bash
# Check for breaking changes against latest published version
concordio breaking \
  --spec petstore.yaml \
  --package-id Contoso.PetStore.Api

# Compare against a specific version
concordio breaking \
  --spec petstore.yaml \
  --package-id Contoso.PetStore.Api \
  --version 1.2.0

# Include prerelease versions
concordio breaking \
  --spec petstore.yaml \
  --package-id Contoso.PetStore.Api \
  --prerelease
```

**Exit codes:**

| Code | Meaning |
|------|---------|
| `0` | No breaking changes detected. |
| `1` | Breaking changes detected. |
| `> 1` | Tool error (e.g., invalid arguments, missing files). |

---

### `concordio get-spec`

Retrieve a specification file from a published contract NuGet package.

```bash
concordio get-spec \
  --package-id <id>
```

**Required options:**

| Option | Description |
|--------|-------------|
| `--package-id` | Package ID of the NuGet package to retrieve the spec from. |

**Optional options:**

| Option | Default | Description |
|--------|---------|-------------|
| `--version` | Latest | Version of the NuGet package. |
| `--prerelease` | `false` | Include prerelease versions. |
| `--kind` | `openapi` | Contract kind: `openapi`, `proto`, or `asyncapi`. |
| `--output-path` | Current directory | Output path for the retrieved file. |
| `--overwrite-output` | `true` | Overwrite the output file if it exists. |
| `--working-directory` | Temp directory | Working directory for downloading the package. |

**Examples:**

```bash
# Get the spec from the latest version
concordio get-spec --package-id Contoso.PetStore.Api

# Get a specific version and save to a custom path
concordio get-spec \
  --package-id Contoso.PetStore.Api \
  --version 1.2.0 \
  --output-path ./specs/petstore.yaml

# Get a Proto specification
concordio get-spec \
  --package-id Contoso.Grpc.Api \
  --kind proto

# Get an AsyncAPI specification
concordio get-spec \
  --package-id Contoso.Events.Api \
  --kind asyncapi
```

---

### `concordio pack`

Generate and pack contract NuGet packages (.nupkg) from specification files. Combines `generate` functionality with `nuget pack` to produce ready-to-publish packages.

```bash
concordio pack \
  --spec <path[:kind]> \
  --package-id <id> \
  --version <semver>
```

**Required options:**

| Option | Description |
|--------|-------------|
| `--spec` | Specification file(s) with optional kind suffix (format: `path[:kind]`). Kind defaults to `openapi`. Valid kinds: `openapi`, `proto`, `asyncapi`. Can be specified multiple times. |
| `--package-id` | Package ID for the generated NuGet package. |
| `--version` | SemVer version for the package. |

**Optional options:**

| Option | Default | Description |
|--------|---------|-------------|
| `--authors` | `ConcordIO` | Package authors. |
| `--description` | Auto-generated | Package description. |
| `--output` | `.` | Output directory for generated files and .nupkg packages. |
| `--client` | `true` | Also generate and pack a client package. |
| `--client-package-id` | `{PackageId}.Client` | Client package ID. |
| `--client-class-name` | Derived from PackageId | Client class name (OpenAPI only). |
| `--nswag-options` | — | Additional NSwag options as `key=value` (OpenAPI only, repeatable). |
| `--client-options` | — | Additional client options as `key=value` (AsyncAPI only, repeatable). |
| `--package-properties` | — | Additional NuSpec metadata as `key=value` (repeatable). |

For OpenAPI client generation, ConcordIO applies these NSwag defaults unless you override them via `--nswag-options`:

- `NSwagJsonLibrary=SystemTextJson`
- `NSwagJsonPolymorphicSerializationStyle=SystemTextJson`
- `NSwagGenerateNullableReferenceTypes=false`
- `NSwagGenerateExceptionClasses=true`

**Examples:**

```bash
# Pack a single OpenAPI spec
concordio pack \
  --spec petstore.yaml \
  --package-id Contoso.PetStore.Api \
  --version 1.0.0

# Pack with client package to a specific output directory
concordio pack \
  --spec api.yaml \
  --package-id Contoso.Api \
  --version 2.0.0 \
  --output ./packages

# Pack multiple specs of different kinds
concordio pack \
  --spec api.yaml:openapi \
  --spec events.yaml:asyncapi \
  --package-id Contoso.Service \
  --version 1.0.0

# Pack contract only (no client)
concordio pack \
  --spec service.proto:proto \
  --package-id Contoso.Grpc.Api \
  --version 1.0.0 \
  --client false
```

**Output:**

The `pack` command produces `.nupkg` files:

1. **Contract package** (`{PackageId}.{Version}.nupkg`) — ready-to-publish NuGet package containing spec files and MSBuild targets.
2. **Client package** (`{PackageId}.Client.{Version}.nupkg`) — if `--client` is enabled, a development dependency package for code generation.

**Exit codes:**

| Code | Meaning |
|------|--------|
| `0` | Packages created successfully. |
| `1` | Error (e.g., invalid arguments, missing spec files, nuget pack failure). |

---

## How It Works

### Contract Packages

The `generate` command creates a NuGet package that:

- Bundles the specification files as content.
- Includes a `.targets` file that exposes specs as `ConcordIOContract` (OpenAPI/Proto) or `ConcordIOAsyncApiContract` (AsyncAPI) MSBuild items.
- Consuming projects automatically see the contract files after installing the package — no file copying needed.

### Client Packages

The client package is a **development dependency** that:

- Declares a dependency on the corresponding contract package.
- Wires the contract specs to code generators at build time:
  - **OpenAPI** → [NSwag](https://github.com/RicoSuter/NSwag) (generates C# client classes)
  - **AsyncAPI** → ConcordIO.AsyncApi.Client (generates messaging client code)
- For **AsyncAPI**, updates `ConcordIOAsyncApiContract` item metadata in-place (MSBuild `Update`) so consuming builds do not duplicate the same contract path.
- Consuming projects just install the client package and get strongly-typed clients generated automatically on build.

### Breaking-Change Detection

The `breaking` command:

1. Downloads the published contract NuGet package.
2. Extracts the specification file.
3. Runs [oasdiff](https://github.com/Tufin/oasdiff) to compare the local spec against the published one.
4. Reports breaking changes with a non-zero exit code, suitable for CI/CD gates.

## Known Limitation: OpenAPI Client Generation in Multi-TFM Consumers

OpenAPI client generation currently relies on the NSwag + `Microsoft.Extensions.ApiDescription.Client` MSBuild orchestration path. In some projects that use `<TargetFrameworks>` (multi-targeting), NSwag can skip generation due to target-ordering behavior in the outer/inner build dispatch.

- **Affected scenario**: Consumer projects using multi-targeting with generated OpenAPI client packages.
- **Observed failure**: Missing generated client types at compile time (for example `CS0246`).
- **Current recommendation**: Prefer single-target projects (`<TargetFramework>`) when consuming generated OpenAPI client packages.
- **Tracking issue**: [#61](https://github.com/LevDevIO/ConcordIO/issues/61).

For automated validation, the E2E suite now uses single-TFM projects with separate coverage for net8.0, net9.0, and net10.0.

## Prerequisites

- **.NET 10 SDK** or later
- **nuget CLI** on `PATH` (required for `breaking` and `get-spec` commands)

## Supported Platforms

The tool bundles oasdiff binaries for:

- Windows x64 / ARM64
- Linux x64 / ARM64
- macOS (universal)

## License

Licensed under the [MIT License](../../LICENSE).
