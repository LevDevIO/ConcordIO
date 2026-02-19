# ❓ Frequently Asked Questions (FAQ)

Common questions about ConcordIO, answered.

## General Questions

### What is ConcordIO?

ConcordIO is a .NET CLI tool and NuGet-based contract management toolchain. It helps teams package API contracts (OpenAPI, AsyncAPI, Protocol Buffers) as NuGet packages, generate clients automatically, and detect breaking changes.

### Is ConcordIO open source?

Yes! ConcordIO is licensed under the Apache License 2.0. Source code is available on [GitHub](https://github.com/LevDevIO/ConcordIO).

### What version of .NET do I need?

.NET 10 SDK or later. The tool itself targets .NET 10, but generated code works with .NET 8, 9, and 10.

### Does ConcordIO work with older .NET versions?

The CLI tool requires .NET 10 to run. However:
- MSBuild task packages support .NET 8, 9, and 10
- Generated clients can target any .NET version that NSwag supports
- Contract packages are framework-agnostic (content-only)

### Is ConcordIO production-ready?

ConcordIO is in active development. Check the [releases page](https://github.com/LevDevIO/ConcordIO/releases) for stability information and version status.

## Installation & Setup

### How do I install ConcordIO?

```bash
dotnet tool install --global ConcordIO.Tool
```

See [Installation Guide](../getting-started/installation.md) for details.

### Can I use ConcordIO without installing globally?

Yes, install as a local tool:

```bash
dotnet new tool-manifest
dotnet tool install ConcordIO.Tool
dotnet concordio --version
```

### Do I need NuGet CLI?

Only for `breaking` and `get-spec` commands. These commands download published packages, which requires `nuget.exe`.

For `generate` and `pack` commands, NuGet CLI is not required.

### What if I don't have nuget.exe?

- **Windows**: `choco install nuget.commandline`
- **macOS**: `brew install nuget`
- **Linux**: Download from [nuget.org](https://www.nuget.org/downloads)

## Contract Packages

### What's the difference between contract and client packages?

- **Contract package**: Contains spec files, no code generation
- **Client package**: Development dependency that generates code from contracts

Consumers typically reference the client package, which automatically pulls in the contract package.

### Can I have multiple specs in one package?

Yes! Use multiple `--spec` options:

```bash
concordio pack \
  --spec api.yaml:openapi \
  --spec events.yaml:asyncapi \
  --spec service.proto:proto \
  --package-id My.Service \
  --version 1.0.0
```

### How do I version my contract packages?

Follow Semantic Versioning (SemVer):
- **Major** version for breaking changes
- **Minor** version for new features (backward compatible)
- **Patch** version for bug fixes

Use `concordio breaking` to detect breaking changes automatically.

### Can I publish to private NuGet feeds?

Yes! Use standard `dotnet nuget push` with your feed URL and credentials:

```bash
dotnet nuget push *.nupkg --source https://your-feed.com/nuget --api-key YOUR_KEY
```

### Do contract packages have dependencies?

Typically no. Contract packages are content-only and don't reference other packages.

Client packages do have dependencies:
- The corresponding contract package
- Code generator packages (NSwag, ConcordIO.AsyncApi.Client, etc.)

## Code Generation

### Why isn't my client code being generated?

Common causes:
1. **Wrong package**: Reference the `.Client` package, not the contract package
2. **Multi-targeting**: OpenAPI generation has issues with `<TargetFrameworks>` — use single `<TargetFramework>`
3. **Build not run**: Generated code appears after `dotnet build`
4. **Cached packages**: Clear NuGet cache: `dotnet nuget locals all --clear`

See [Common Issues](./common-issues.md#code-generation-issues) for details.

### Can I customize generated clients?

Yes! For OpenAPI clients, use MSBuild target to update `OpenApiReference` metadata:

```xml
<Target Name="CustomizeClient" AfterTargets="ConcordIOAddOpenApiReferenceForNSwag">
  <ItemGroup>
    <OpenApiReference Update="@(OpenApiReference)">
      <Namespace>MyApp.Clients</Namespace>
      <NSwagGenerateClientInterfaces>true</NSwagGenerateClientInterfaces>
    </OpenApiReference>
  </ItemGroup>
</Target>
```

See the [Consuming Contract Tutorial](../tutorials/consuming-contract.md) for detailed examples.

### Where is the generated code?

**OpenAPI**: Depends on NSwag configuration, typically in `obj/` or `Generated/`

**AsyncAPI**: `obj/{Configuration}/{TargetFramework}/ConcordIO.AsyncApi.Generated/`

Generated files are included in compilation automatically.

### Can I commit generated code to source control?

Not recommended. Generated code should be created during build, not checked in. Add to `.gitignore`:

```gitignore
**/obj/
**/ConcordIO.AsyncApi.Generated/
```

### Why use automatic generation instead of pre-generated SDKs?

**Benefits**:
- Always in sync with contract
- No SDK maintenance burden
- Guaranteed consistency across consumers
- Instant updates when contracts change

## Breaking Changes

### What counts as a breaking change?

For OpenAPI:
- Removing endpoints or operations
- Removing or renaming parameters
- Changing parameter types
- Removing response fields
- Making optional fields required
- Removing enum values

See the [CLI Tool Guide](../../src/ConcordIO.Tool/README.md) for the `breaking` command usage.

### How does breaking change detection work?

ConcordIO uses [oasdiff](https://github.com/Tufin/oasdiff) under the hood:

1. Downloads published contract package
2. Extracts the spec
3. Compares with your local spec
4. Reports breaking changes

### Can I customize what's considered breaking?

You can pass additional options to oasdiff:

```bash
concordio breaking \
  --spec api.yaml \
  --package-id My.Api \
  --cli-options deprecation-days-stable=30
```

See oasdiff documentation for available options.

### What if breaking changes are unavoidable?

1. **Bump major version**: Indicates breaking change to consumers
2. **Document changes**: Add migration guide
3. **Support old version**: Keep old version available temporarily
4. **Use API versioning**: Consider `/v1/` and `/v2/` endpoints

### Can I use breaking detection with AsyncAPI or Proto?

Currently, breaking detection only supports OpenAPI. AsyncAPI and Proto support is planned for future releases.

## AsyncAPI

### What's the difference between AsyncAPI Server and Client?

- **ConcordIO.AsyncApi.Server**: Generates AsyncAPI specs from .NET types (producer side)
- **ConcordIO.AsyncApi.Client**: Generates .NET types from AsyncAPI specs (consumer side)

### Do I need MassTransit to use AsyncAPI packages?

The packages are designed for MassTransit but don't require it. The generated AsyncAPI documents use MassTransit conventions (URN addresses), and generated types work well with MassTransit, but you can use them independently.

### How do I specify which types to include in AsyncAPI?

Use MSBuild properties with namespace patterns:

```xml
<PropertyGroup>
  <ConcordIOEventTypes>MyApp.Events.*</ConcordIOEventTypes>
  <ConcordIOCommandTypes>MyApp.Commands.*</ConcordIOCommandTypes>
</PropertyGroup>
```

See [AsyncAPI Server Package](../../src/ConcordIO.AsyncApi.Server/README.md) for details.

### Why JSON instead of YAML for AsyncAPI?

JSON is more reliable for programmatic consumption. Some edge cases cause YAML serialization errors. JSON is equivalent in the AsyncAPI 3.0 spec.

You can change the format:

```xml
<PropertyGroup>
  <ConcordIOAsyncApiOutputFormat>yaml</ConcordIOAsyncApiOutputFormat>
</PropertyGroup>
```

### Can I include AsyncAPI specs in a contract package?

Yes! Use the `concordio pack` command with `--spec file.yaml:asyncapi`, or use `ConcordIO.AsyncApi.Server` package which includes specs in packages automatically.

## OpenAPI

### What's the difference between ConcordIO and NSwag?

ConcordIO **uses** NSwag for OpenAPI client generation. ConcordIO adds:
- Contract package management
- NuGet distribution
- Breaking change detection
- Multi-spec support
- Consistent workflow across protocol types

### Can I use my existing NSwag configuration?

Yes! ConcordIO client packages create `OpenApiReference` items that NSwag consumes. Your existing NSwag setup should work with minimal changes.

### Do I need to learn NSwag?

Basic usage works without NSwag knowledge. For advanced customization, understanding NSwag options helps. See the [CLI Tool Guide](../../src/ConcordIO.Tool/README.md) for NSwag defaults and the [Consuming Contract Tutorial](../tutorials/consuming-contract.md) for customization examples.

### Why do multi-target projects have issues?

NSwag's MSBuild integration sometimes skips generation during outer/inner build dispatch with `<TargetFrameworks>`. This is a known NSwag limitation, not specific to ConcordIO.

**Workaround**: Use single `<TargetFramework>` for projects consuming OpenAPI clients.

See [Known Limitations](./known-limitations.md#openapi-multi-tfm).

## Protocol Buffers

### Does ConcordIO generate gRPC clients?

ConcordIO packages Proto files but doesn't generate clients itself. Consumers use `Grpc.Tools` for code generation.

**Why**: Proto/gRPC tooling is mature and standardized. ConcordIO focuses on distribution and versioning.

### Can I use Proto contracts with non-.NET consumers?

Yes! Proto files are language-agnostic. Publish the contract package and extract the `.proto` files for use with any language.

### How do I extract Proto files from a package?

```bash
concordio get-spec --package-id My.Grpc --kind proto --output-path service.proto
```

## CI/CD & Automation

### How do I use ConcordIO in CI/CD pipelines?

The CLI is designed for automation:
- Install as dotnet tool in pipeline
- Use exit codes for conditional logic
- Pass options via command-line flags

See [Tutorial: CI/CD Setup](../tutorials/cicd-setup.md).

### Can I enforce breaking change policies?

Yes! Add a CI step that runs `concordio breaking` and fails the build if exit code is 1:

```yaml
- name: Check breaking changes
  run: |
    concordio breaking --spec api.yaml --package-id My.Api
    if [ $? -eq 1 ]; then
      echo "Breaking changes detected!"
      exit 1
    fi
```

### Should I automate version bumping?

Recommended approach:
1. Use `concordio breaking` to detect type of change
2. Use tools like GitVersion or semantic-release for version bumping
3. Trigger package generation and publishing

See the [CI/CD Tutorial](../tutorials/cicd-setup.md) for a complete implementation.

## Troubleshooting

### Where can I find detailed logs?

Build with diagnostic verbosity:

```bash
dotnet build -v diag > build.log
```

Search for "ConcordIO" or "NSwag" in the log.

### How do I report a bug?

1. Check [existing issues](https://github.com/LevDevIO/ConcordIO/issues)
2. If new, open an issue with:
   - ConcordIO version (`concordio --version`)
   - .NET SDK version (`dotnet --version`)
   - Complete error message
   - Minimal reproduction steps

### Where can I get help?

1. [FAQ](./faq.md) (you are here!)
2. [Common Issues](./common-issues.md)
3. [GitHub Issues](https://github.com/LevDevIO/ConcordIO/issues)
4. [GitHub Discussions](https://github.com/LevDevIO/ConcordIO/discussions)

## Contributing

### How can I contribute?

See [Contributing Guide](../../CONTRIBUTING.md) for:
- Code contributions
- Documentation improvements
- Bug reports
- Feature requests

### Can I add support for other spec types?

Yes! ConcordIO is designed to be extensible. Open an issue to discuss the spec type before implementing.

## Next Steps

- [🚀 Quick Start Guide](../getting-started/quick-start.md)
- [🐛 Common Issues](./common-issues.md)
- [⚠️ Known Limitations](./known-limitations.md)
- [📖 Full Documentation](../README.md)
