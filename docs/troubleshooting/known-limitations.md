# ⚠️ Known Limitations

Current limitations and workarounds for ConcordIO.

## OpenAPI Limitations

### <a name="openapi-multi-tfm"></a>Multi-Target Framework Support

**Limitation**: OpenAPI client generation may not work reliably in projects using `<TargetFrameworks>` (multi-targeting).

**Affected**: Projects consuming OpenAPI client packages with multiple target frameworks

**Symptom**: Generated client types are missing, resulting in compilation errors (e.g., `CS0246: The type or namespace name 'MyApiClient' could not be found`).

**Cause**: NSwag's MSBuild integration can skip generation during the outer/inner build dispatch that occurs with multi-targeting. This is a known NSwag limitation, not specific to ConcordIO.

**Workaround**: Use single `<TargetFramework>` instead:

```xml
<!-- ❌ Problematic -->
<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>

<!-- ✅ Recommended -->
<TargetFramework>net8.0</TargetFramework>
```

If you need coverage for multiple frameworks, create separate test projects with single TFMs:
- `MyProject.Tests.Net8` (targets net8.0)
- `MyProject.Tests.Net9` (targets net9.0)
- `MyProject.Tests.Net10` (targets net10.0)

**Status**: Tracked in [Issue #61](https://github.com/LevDevIO/ConcordIO/issues/61)

**Future**: Investigating alternative MSBuild wiring to avoid this limitation.

### Breaking Change Detection for OpenAPI Only

**Limitation**: The `breaking` command only supports OpenAPI specifications.

**Affected**: AsyncAPI and Protocol Buffer contracts

**Workaround**: Manual comparison or use spec-specific tools:
- AsyncAPI: Manual diff or custom tooling
- Proto: `buf breaking` command from [Buf](https://buf.build)

**Future**: AsyncAPI and Proto breaking detection support is planned.

### OpenAPI 2.0 (Swagger) Support

**Limitation**: ConcordIO is optimized for OpenAPI 3.x. OpenAPI 2.0 (Swagger) support depends on NSwag.

**Recommendation**: Migrate to OpenAPI 3.x for best results.

**Workaround**: Use NSwag directly if you must use OpenAPI 2.0.

## AsyncAPI Limitations

### YAML Serialization Issues

**Limitation**: YAML output format can fail with certain schema structures (e.g., empty `JsonObject` nodes).

**Symptom**: Build error:
```
YamlDotNet.Core.SyntaxErrorException: Expected SCALAR, SEQUENCE-START, MAPPING-START, or ALIAS, got MappingEnd
```

**Workaround**: Use JSON output format (this is now the default):

```xml
<PropertyGroup>
  <ConcordIOAsyncApiOutputFormat>json</ConcordIOAsyncApiOutputFormat>
</PropertyGroup>
```

JSON is equivalent to YAML in AsyncAPI 3.0 spec and more reliable for programmatic use.

**Status**: JSON is the recommended and default format.

### AsyncAPI 2.x Support

**Limitation**: ConcordIO generates AsyncAPI 3.x documents only.

**Reason**: AsyncAPI 3.x is the latest spec with significant improvements over 2.x.

**Workaround**: Use AsyncAPI converter tools to downgrade if needed (not recommended).

**Future**: No plans to support AsyncAPI 2.x. Focus is on 3.x and future versions.

### MassTransit-Specific Conventions

**Limitation**: Generated AsyncAPI documents use MassTransit URN conventions for channel addresses.

**Impact**: Documents describe message schemas but not actual broker topology (queue names, exchanges, server URLs, etc.).

**Example**:
```yaml
channels:
  MyEvent:
    address: "urn:message:MyApp.Contracts:MyEvent"  # Not a real broker address
```

**Reason**: By design. Contract packages define message shapes, not hosting infrastructure. Broker topology is determined at runtime by MassTransit.

**Workaround**: If you need actual broker topology, maintain separate documentation or use MassTransit configuration files.

### Type Discovery Limitations

**Limitation**: Type discovery patterns are namespace-based and don't support complex queries.

**Example**: You can't select "all types with a specific attribute" directly.

**Workaround**: Organize types into namespaces that match discovery patterns:

```csharp
// Group by purpose
namespace MyApp.Contracts.Events { }  // ConcordIOEventTypes=MyApp.Contracts.Events.*
namespace MyApp.Contracts.Commands { }  // ConcordIOCommandTypes=MyApp.Contracts.Commands.*
```

**Future**: Attribute-based discovery is under consideration.

## Protocol Buffers Limitations

### No Client Generation

**Limitation**: ConcordIO packages Proto files but doesn't generate gRPC clients.

**Reason**: Proto/gRPC tooling is mature and standardized (`Grpc.Tools`). ConcordIO focuses on distribution and versioning.

**Workaround**: Consumers use `Grpc.Tools` for code generation:

```xml
<ItemGroup>
  <PackageReference Include="Grpc.Tools" Version="2.x.x" PrivateAssets="All" />
  <Protobuf Include="@(ConcordIOContract)" Condition="'%(ConcordIOContract.Kind)' == 'proto'" />
</ItemGroup>
```

**Future**: May provide `.Client` packages that wire to `Grpc.Tools` automatically.

### No Breaking Change Detection

**Limitation**: `breaking` command doesn't support Proto files.

**Workaround**: Use [Buf](https://buf.build) for Proto breaking detection:

```bash
buf breaking --against '.git#branch=main'
```

**Future**: Native Proto breaking detection planned.

## Package Management Limitations

### NuGet CLI Required for Some Commands

**Limitation**: `breaking` and `get-spec` commands require `nuget.exe` to be on PATH.

**Reason**: These commands download packages using NuGet CLI.

**Workaround**: Install NuGet CLI or use `dotnet nuget` alternatives (future improvement).

**Future**: May replace with .NET native package download APIs.

### Package Size

**Limitation**: Large OpenAPI specs or multiple specs can result in larger packages.

**Impact**: Slower package restore times.

**Mitigation**:
- Keep specs concise
- Split large specs into multiple packages
- Use package compression (automatic with NuGet)

### Version Immutability

**Limitation**: Once published, NuGet packages are immutable. You can't update a version.

**Reason**: NuGet design for reproducibility.

**Workaround**: Publish a new version. Use pre-release tags for iteration:
- `1.0.0-preview.1`
- `1.0.0-preview.2`
- `1.0.0` (final)

## MSBuild Limitations

### Task Runtime Selection

**Limitation**: MSBuild task packages must target specific .NET versions.

**Current Support**: net8.0, net9.0, net10.0

**Impact**: If you use an older .NET SDK (e.g., .NET 6), MSBuild tasks may not load.

**Workaround**: Upgrade to .NET 8 SDK or later.

**Future**: May add net6.0 support if there's demand.

### Restore-Time Generation Not Supported

**Limitation**: Code generation happens during build, not restore.

**Impact**: IDE IntelliSense may not show generated types until after first build.

**Workaround**: Build project once to generate types, then IntelliSense will work.

**Future**: Exploring design-time builds for better IDE support.

### Transitive Dependencies

**Limitation**: Generated code dependencies (e.g., System.ComponentModel.Annotations) must be added manually if you enable certain features.

**Example**: AsyncAPI client with data annotations requires:
```xml
<PackageReference Include="System.ComponentModel.Annotations" Version="x.x.x" />
```

**Future**: May auto-inject dependencies via MSBuild.

## Platform Limitations

### Windows ARM64 oasdiff Binary

**Limitation**: oasdiff binary for Windows ARM64 is bundled but not extensively tested.

**Impact**: `breaking` command may not work on Windows ARM64 devices.

**Workaround**: Use x64 emulation or run on x64 machine.

**Future**: Will improve testing coverage for ARM64.

### Linux Distro-Specific Issues

**Limitation**: oasdiff binaries are built for generic Linux. Some distros may have compatibility issues.

**Workaround**: Ensure required libraries are installed (usually `libc6`).

**Future**: May provide distro-specific binaries if needed.

## CI/CD Limitations

### Manual NuGet Feed Configuration

**Limitation**: ConcordIO doesn't auto-configure NuGet sources.

**Impact**: CI pipelines must set up feeds explicitly.

**Workaround**: Use `NuGet.config` in repo or configure in CI:

```yaml
- name: Add NuGet source
  run: dotnet nuget add source https://your-feed.com/nuget --name myfeed
```

### Exit Code Only for Breaking Detection

**Limitation**: `breaking` command returns exit code but limited structured output.

**Impact**: Difficult to parse details programmatically in CI.

**Workaround**: Parse stdout/stderr or use oasdiff directly for more control.

**Future**: JSON output mode planned for better CI integration.

## Documentation Limitations

### No Built-in API Documentation Portal

**Limitation**: ConcordIO packages specs but doesn't provide a documentation portal.

**Workaround**: 
- Use Swagger UI for OpenAPI
- Use AsyncAPI Studio for AsyncAPI
- Host specs on documentation sites

**Future**: May provide portal integration in the future.

### Limited Metadata in Packages

**Limitation**: Generated package metadata is basic (authors, description, version).

**Workaround**: Use `--package-properties` to add custom metadata:

```bash
concordio pack \
  --spec api.yaml \
  --package-id My.Api \
  --version 1.0.0 \
  --package-properties "RepositoryUrl=https://github.com/my/repo" \
  --package-properties "Tags=api;rest;contracts"
```

## Security Limitations

### No Spec Validation Before Packaging

**Limitation**: ConcordIO doesn't validate specs before packaging (except basic file existence).

**Impact**: Invalid specs can be packaged and published.

**Recommendation**: Validate specs as part of your CI/CD:

```bash
# OpenAPI
npx @stoplight/spectral-cli lint api.yaml

# AsyncAPI
npx @asyncapi/cli validate events.yaml

# Proto
buf lint
```

**Future**: May add opt-in validation.

### No Package Signing

**Limitation**: ConcordIO doesn't automatically sign NuGet packages.

**Workaround**: Sign packages after generation:

```bash
nuget sign MyPackage.1.0.0.nupkg -CertificatePath cert.pfx
```

**Future**: May add signing support to `pack` command.

## Performance Limitations

### Large Spec Processing

**Limitation**: Very large specs (>10MB) may slow down generation and builds.

**Mitigation**:
- Split into multiple packages
- Use refs/`$ref` to modularize specs
- Optimize spec structure

### Parallel Builds

**Limitation**: Code generation tasks may not fully utilize parallel builds.

**Impact**: Multi-project solutions may not build as fast as they could.

**Future**: Investigating MSBuild parallelization improvements.

## Reporting Limitations

If you encounter a limitation not listed here:

1. Check if there's an [existing issue](https://github.com/LevDevIO/ConcordIO/issues)
2. If not, open a new issue with:
   - Description of the limitation
   - Impact on your workflow
   - Potential workarounds you've tried
   - Use case details

## Next Steps

- [❓ FAQ](./faq.md) - Common questions
- [🐛 Common Issues](./common-issues.md) - Troubleshooting
- [🚀 Quick Start Guide](../getting-started/quick-start.md) - Get started despite limitations
- [GitHub Issues](https://github.com/LevDevIO/ConcordIO/issues) - Report or track issues
