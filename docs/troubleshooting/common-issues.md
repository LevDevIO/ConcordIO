# 🐛 Common Issues and Solutions

Quick solutions to common problems when using ConcordIO.

## Installation Issues

### Issue: "concordio: command not found"

**Symptom**: After installing the tool, running `concordio` shows command not found.

**Cause**: .NET tools directory is not in your PATH.

**Solution**:

Add to PATH (Linux/macOS):
```bash
export PATH="$PATH:$HOME/.dotnet/tools"
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.bashrc  # or ~/.zshrc
```

Add to PATH (Windows PowerShell):
```powershell
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
```

Or use full path:
```bash
~/.dotnet/tools/concordio --version
```

### Issue: "A newer version of the tool is already installed"

**Symptom**: Installation fails with version conflict message.

**Solution**:

Update instead of install:
```bash
dotnet tool update --global ConcordIO.Tool
```

Or uninstall first:
```bash
dotnet tool uninstall --global ConcordIO.Tool
dotnet tool install --global ConcordIO.Tool
```

### Issue: "nuget: command not found"

**Symptom**: `breaking` or `get-spec` commands fail because nuget CLI is missing.

**Cause**: The `nuget` CLI is required for package downloads.

**Solution**:

Install NuGet CLI:
```bash
# Windows (Chocolatey)
choco install nuget.commandline

# macOS (Homebrew)
brew install nuget

# Linux (via Mono + nuget.exe + wrapper)
# 1. Install Mono (example for Debian/Ubuntu)
sudo apt-get update
sudo apt-get install -y mono-runtime

# 2. Download nuget.exe to a user-local directory
mkdir -p "$HOME/.nuget-cli"
wget -O "$HOME/.nuget-cli/nuget.exe" https://dist.nuget.org/win-x86-commandline/latest/nuget.exe

# 3. Create a 'nuget' wrapper script
cat > "$HOME/.nuget-cli/nuget" << 'EOF'
#!/usr/bin/env bash
exec mono "$HOME/.nuget-cli/nuget.exe" "$@"
EOF
chmod +x "$HOME/.nuget-cli/nuget"

# 4. Add to PATH for the current shell session
export PATH="$HOME/.nuget-cli:$PATH"
# (Add to ~/.bashrc or ~/.zshrc for persistence)
```

## Code Generation Issues

### Issue: Missing Generated Client Types

**Symptom**: After adding client package and building, types like `MyApiClient` are not available.

**Causes and Solutions**:

#### Cause 1: Wrong Package Referenced

❌ Wrong:
```xml
<PackageReference Include="MyApi.Contract" Version="1.0.0" />
```

✅ Correct:
```xml
<PackageReference Include="MyApi.Contract.Client" Version="1.0.0" />
```

The `.Client` package triggers code generation.

#### Cause 2: Multi-Target Framework (OpenAPI only)

❌ Problematic:
```xml
<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
```

✅ Workaround:
```xml
<TargetFramework>net8.0</TargetFramework>
```

NSwag has issues with multi-TFM projects. Use single target. See [Known Limitations](./known-limitations.md#openapi-multi-tfm).

#### Cause 3: Build Not Run

Generate code requires a build:
```bash
dotnet restore  # Restore packages
dotnet build    # Generate code
```

#### Cause 4: Cached Packages

Clear NuGet cache:
```bash
dotnet nuget locals all --clear
dotnet restore
dotnet build
```

### Issue: AsyncAPI Types Not Generated

**Symptom**: No `.g.cs` files created after adding `ConcordIO.AsyncApi.Client`.

**Diagnostic Steps**:

1. **Verify contract items exist**:
```bash
dotnet build -v n 2>&1 | grep ConcordIOAsyncApiContract
```

Look for: `ConcordIO.Client: AsyncAPI files: 1`

2. **Check package references**:
```xml
<!-- Need both -->
<PackageReference Include="MyContract.Package" Version="1.0.0" />
<PackageReference Include="ConcordIO.AsyncApi.Client" Version="0.1.0" />
```

3. **Verbose logging**:
```bash
dotnet build -v diag > build.log
# Search for "ConcordIO" in build.log
```

**Common Causes**:

- Contract package not referenced
- Contract package doesn't expose `ConcordIOAsyncApiContract` items
- MSBuild task failed (check build log)

### Issue: "MetadataLoadContext has been disposed"

**Symptom**: Task instantiation error with AsyncAPI client generation.

```
The "ConcordIO.AsyncApi.Client.Tasks.GenerateContractsTask" task could not be instantiated...
MetadataLoadContext that created it has been disposed.
```

**Cause**: Older package versions selected task assembly incorrectly.

**Solution**: Upgrade to latest version:
```bash
dotnet add package ConcordIO.AsyncApi.Client --version <latest>
```

## Breaking Change Detection Issues

### Issue: "Failed to download package"

**Symptom**: `breaking` command fails with package download error.

**Causes and Solutions**:

#### Cause 1: Package Doesn't Exist

Verify package exists:
```bash
dotnet nuget search MyApi.Contract
```

Or check your NuGet feed manually.

#### Cause 2: Wrong Package Source

Add the correct source:
```bash
dotnet nuget add source https://your-feed.com/nuget --name myfeed
```

Or use `NuGet.config`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="myfeed" value="https://your-feed.com/nuget" />
  </packageSources>
</configuration>
```

#### Cause 3: Authentication Required

Add credentials:
```bash
dotnet nuget add source https://your-feed.com/nuget \
  --name myfeed \
  --username USER \
  --password PASS \
  --store-password-in-clear-text
```

### Issue: "oasdiff: not found" or Execution Error

**Symptom**: Breaking command fails with oasdiff error.

**Cause**: Bundled oasdiff binary not found or not executable.

**Solution**:

1. Verify ConcordIO version is up-to-date
2. Check file permissions (Linux/macOS):
```bash
# Find oasdiff location
find ~/.dotnet/tools/.store/concordio.tool -name "oasdiff*"

# Make executable if needed
chmod +x <path-to-oasdiff>
```

### Issue: "No breaking changes detected" but there are changes

**Symptom**: `breaking` returns exit code 0 but you made breaking changes.

**Possible Causes**:

1. **Wrong spec file**: Ensure you're comparing the correct file
2. **Wrong package version**: Ensure you're comparing against the right version
3. **Non-breaking changes**: Your changes might not be breaking (e.g., adding fields)

**Verify**:
```bash
# Specify exact version
concordio breaking --spec api.yaml --package-id My.Api --version 1.0.0

# Get published spec to compare manually
concordio get-spec --package-id My.Api --output-path published.yaml
diff api.yaml published.yaml
```

## Package Generation Issues

### Issue: "Spec file not found"

**Symptom**: `generate` or `pack` command fails with file not found.

**Cause**: Incorrect file path or working directory.

**Solution**:

Use absolute or relative paths:
```bash
# Relative to current directory
concordio pack --spec ./specs/api.yaml --package-id My.Api --version 1.0.0

# Absolute path
concordio pack --spec /full/path/to/api.yaml --package-id My.Api --version 1.0.0
```

Verify file exists:
```bash
ls -la specs/api.yaml
```

### Issue: "Invalid package ID"

**Symptom**: Package generation fails with invalid package ID error.

**Cause**: Package ID doesn't follow NuGet naming rules.

**Solution**:

Valid package IDs:
- ✅ `MyCompany.MyProduct.Api`
- ✅ `Contoso.PetStore.Contracts`
- ❌ `my-package` (no hyphens)
- ❌ `My Package` (no spaces)

Rules:
- Use only alphanumerics and dots
- No spaces or special characters (except dots)
- Case-insensitive

### Issue: Pack Command Fails with NuGet Error

**Symptom**: `concordio pack` succeeds but no `.nupkg` files created.

**Cause**: Check command output for errors.

**Diagnostic**:
```bash
concordio pack --spec api.yaml --package-id My.Api --version 1.0.0
# Look for error messages in output
```

**Common Causes**:
- Output directory doesn't exist
- Permissions issue
- Disk space

**Solution**:
```bash
# Create output directory
mkdir -p ./packages

# Specify output
concordio pack --spec api.yaml --package-id My.Api --version 1.0.0 --output ./packages

# Check results
ls -la ./packages/*.nupkg
```

## MSBuild Integration Issues

### Issue: "Target does not exist in the project"

**Symptom**: Custom MSBuild target fails with target not found error.

**Cause**: Target depends on ConcordIO targets that aren't loaded yet.

**Solution**:

Use `AfterTargets` instead of `DependsOnTargets`:

❌ Wrong:
```xml
<Target Name="MyTarget" DependsOnTargets="ConcordIOAddOpenApiReferenceForNSwag">
```

✅ Correct:
```xml
<Target Name="MyTarget" AfterTargets="ConcordIOAddOpenApiReferenceForNSwag">
```

### Issue: Custom Properties Not Applied

**Symptom**: MSBuild properties for ConcordIO packages are ignored.

**Cause**: Properties defined after packages are restored.

**Solution**:

Define properties before `<Import>` or package restore:

✅ Correct order:
```xml
<PropertyGroup>
  <ConcordIOClientClassStyle>Record</ConcordIOClientClassStyle>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="ConcordIO.AsyncApi.Client" Version="0.1.0" />
</ItemGroup>
```

❌ Wrong order:
```xml
<ItemGroup>
  <PackageReference Include="ConcordIO.AsyncApi.Client" Version="0.1.0" />
</ItemGroup>

<PropertyGroup>
  <ConcordIOClientClassStyle>Record</ConcordIOClientClassStyle>  <!-- Too late -->
</PropertyGroup>
```

## Runtime Issues

### Issue: Generated Client Throws Exceptions

**Symptom**: Using generated OpenAPI client throws serialization or HTTP errors.

**Common Causes**:

#### Cause 1: Base URL Not Set

```csharp
// ❌ Missing base URL
var client = new MyApiClient(httpClient);

// ✅ Set base URL
var httpClient = new HttpClient { BaseAddress = new Uri("https://api.example.com") };
var client = new MyApiClient(httpClient);
```

#### Cause 2: JSON Serialization Mismatch

Ensure client and server use compatible serialization:

```xml
<Target Name="UseNewtonsoftJson" AfterTargets="ConcordIOAddOpenApiReferenceForNSwag">
  <ItemGroup>
    <OpenApiReference Update="@(OpenApiReference)">
      <NSwagJsonLibrary>NewtonsoftJson</NSwagJsonLibrary>
    </OpenApiReference>
  </ItemGroup>
</Target>
```

#### Cause 3: Certificate Validation (HTTPS)

For development/testing:
```csharp
var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};
var httpClient = new HttpClient(handler);
```

⚠️ Don't use in production!

### Issue: AsyncAPI Generated Types Have Compilation Errors

**Symptom**: Build fails with errors in generated `.g.cs` files.

**Common Causes**:

#### Cause 1: Namespace Conflicts

Use `x-dotnet-namespace` in AsyncAPI spec:
```yaml
components:
  schemas:
    MyType:
      type: object
      x-dotnet-namespace: MyApp.Contracts.Events
```

#### Cause 2: Invalid Schema

Validate your AsyncAPI document:
```bash
# Use online validator or AsyncAPI CLI
asyncapi validate events.yaml
```

#### Cause 3: Missing References

Ensure external types are referenced:
```xml
<ItemGroup>
  <ProjectReference Include="..\Shared.Types\Shared.Types.csproj" />
</ItemGroup>
```

## Performance Issues

### Issue: Build Times Increased After Adding ConcordIO

**Symptom**: Significantly longer build times.

**Causes and Solutions**:

#### OpenAPI Generation (NSwag)

NSwag can be slow for large specs. Optimize:

1. **Use code file caching**:
```xml
<OpenApiReference Include="..." OutputPath="Generated\Client.cs" />
```
NSwag skips regeneration if spec unchanged.

2. **Exclude from incremental builds**:
```xml
<PropertyGroup>
  <GenerateOpenApiClient Condition="'$(Configuration)' == 'Debug'">false</GenerateOpenApiClient>
</PropertyGroup>
```

#### AsyncAPI Generation

Task loads assemblies for type resolution. Optimize:

1. **Limit type patterns**:
```xml
<!-- ❌ Too broad -->
<ConcordIOEventTypes>MyApp.**</ConcordIOEventTypes>

<!-- ✅ More specific -->
<ConcordIOEventTypes>MyApp.Contracts.Events.*</ConcordIOEventTypes>
```

2. **Conditional generation**:
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <ConcordIOEventTypes></ConcordIOEventTypes>  <!-- Skip in Debug -->
</PropertyGroup>
```

## Getting More Help

If your issue isn't covered here:

1. Check [FAQ](./faq.md)
2. Check [Known Limitations](./known-limitations.md)
3. Enable verbose logging:
```bash
dotnet build -v diag > build.log
```
4. Search [GitHub Issues](https://github.com/LevDevIO/ConcordIO/issues)
5. Open a new issue with:
   - ConcordIO version
   - .NET SDK version
   - Complete error message
   - Minimal reproduction
   - Build log (if relevant)

## Next Steps

- [❓ FAQ](./faq.md)
- [⚠️ Known Limitations](./known-limitations.md)
- [🚀 Quick Start Guide](../getting-started/quick-start.md)
- [GitHub Issues](https://github.com/LevDevIO/ConcordIO/issues)
