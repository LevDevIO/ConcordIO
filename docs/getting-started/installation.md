# 📦 Installation Guide

Complete installation instructions for ConcordIO tools and packages.

## Prerequisites

### Required

- **.NET 10 SDK** or later
  - Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
  - Verify: `dotnet --version`

### Optional (for specific features)

- **nuget CLI** - Required for `breaking` and `get-spec` commands
  - Windows: `choco install nuget.commandline`
  - macOS: `brew install nuget`
  - Linux: Download from [nuget.org](https://www.nuget.org/downloads)

## Installing the CLI Tool

### Global Installation (Recommended)

Install the tool globally so it's available from any directory:

```bash
dotnet tool install --global ConcordIO.Tool
```

Verify the installation:

```bash
concordio --version
```

### Update Global Tool

Update to the latest version:

```bash
dotnet tool update --global ConcordIO.Tool
```

### Uninstall Global Tool

```bash
dotnet tool uninstall --global ConcordIO.Tool
```

### Local Installation (Project-Specific)

For project-specific tooling, install as a local tool:

```bash
# Create a tool manifest if it doesn't exist
dotnet new tool-manifest

# Install the tool locally
dotnet tool install ConcordIO.Tool
```

Run with `dotnet` prefix:

```bash
dotnet concordio --version
```

Update local tool:

```bash
dotnet tool update ConcordIO.Tool
```

### Version Pinning

Pin to a specific version in your project:

```bash
dotnet tool install ConcordIO.Tool --version 1.2.3
```

Or in `.config/dotnet-tools.json`:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "concordio.tool": {
      "version": "1.2.3",
      "commands": [
        "concordio"
      ]
    }
  }
}
```

## Installing MSBuild Packages

### AsyncAPI Client Package

For generating C# types from AsyncAPI specifications:

```xml
<ItemGroup>
  <PackageReference Include="ConcordIO.AsyncApi.Client" Version="0.1.0">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>
```

### AsyncAPI Server Package

For generating AsyncAPI specifications from .NET types:

```xml
<ItemGroup>
  <PackageReference Include="ConcordIO.AsyncApi.Server" Version="0.1.0">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>
```

## Package Sources

### GitHub Packages (Default)

ConcordIO packages are published to GitHub Packages by default. Add the source:

```bash
dotnet nuget add source https://nuget.pkg.github.com/LevDevIO/index.json \
  --name github-levdevio
```

Or in `NuGet.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="github-levdevio" value="https://nuget.pkg.github.com/LevDevIO/index.json" />
  </packageSources>
</configuration>
```

For this public repository, consuming packages from GitHub Packages does not require authentication.

### NuGet.org (Public Releases)

Stable releases are published to NuGet.org and don't require additional configuration:

```bash
dotnet tool install --global ConcordIO.Tool
```

## Platform-Specific Notes

### Windows

No special configuration needed. Ensure:
- .NET SDK is installed
- PowerShell or Command Prompt is used
- PATH includes .NET tools: `%USERPROFILE%\.dotnet\tools`

### macOS

Ensure tools are on PATH:

```bash
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.zshrc
source ~/.zshrc
```

For Apple Silicon (M1/M2):
- .NET SDK includes ARM64 support
- ConcordIO bundles universal macOS binaries

### Linux

Add tools to PATH in `.bashrc` or `.zshrc`:

```bash
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.bashrc
source ~/.bashrc
```

### Docker

Dockerfile example:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0

# Install ConcordIO
RUN dotnet tool install --global ConcordIO.Tool

# Add to PATH
ENV PATH="$PATH:/root/.dotnet/tools"

# Verify installation
RUN concordio --version
```

## Offline Installation

For environments without internet access:

1. Download the package on an online machine:

```bash
dotnet tool install --global ConcordIO.Tool --tool-path ./tools
```

2. Copy `./tools` to the offline machine

3. Install from local files:

```bash
dotnet tool install --global ConcordIO.Tool --add-source ./tools
```

## Troubleshooting Installation

### Tool not found after installation

**Symptom**: `concordio: command not found`

**Solution**: Add .NET tools to PATH:

```bash
# Linux/macOS
export PATH="$PATH:$HOME/.dotnet/tools"

# Windows (PowerShell)
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
```

### Permission denied (Linux/macOS)

**Symptom**: `Permission denied` when installing

**Solution**: Don't use `sudo` with `dotnet tool install`. It should install to your user directory.

### Version conflicts

**Symptom**: `A newer version of the tool is already installed`

**Solution**: Update instead of install:

```bash
dotnet tool update --global ConcordIO.Tool
```

Or uninstall first:

```bash
dotnet tool uninstall --global ConcordIO.Tool
dotnet tool install --global ConcordIO.Tool
```

### Package source authentication issues

**Symptom**: `401 Unauthorized` when restoring packages

**Solution**: For private feeds, configure authentication:

```bash
dotnet nuget add source https://your-feed.com/nuget \
  --name private-feed \
  --username USERNAME \
  --password PASSWORD \
  --store-password-in-clear-text
```

## Verifying Installation

Check everything is working:

```bash
# CLI tool
concordio --version

# .NET SDK
dotnet --version

# nuget CLI (optional)
nuget help

# List installed tools
dotnet tool list --global
```

## Next Steps

- [🚀 Quick Start Guide](./quick-start.md) - Get started in 5 minutes
- [🎯 When to Use ConcordIO](./when-to-use.md) - Understanding use cases
- [🏗️ Core Concepts](./concepts.md) - Learn the fundamentals
- [🛠️ CLI Tool Guide](../user-guides/cli-tool.md) - Complete CLI reference

## Getting Help

- [❓ FAQ](../troubleshooting/faq.md)
- [🐛 Common Issues](../troubleshooting/common-issues.md)
- [GitHub Issues](https://github.com/LevDevIO/ConcordIO/issues)
