# 🚀 Quick Start Guide

Get up and running with ConcordIO in 5 minutes! This guide walks you through installing the tool and publishing your first API contract.

## Prerequisites

- ✅ **.NET 10 SDK** or later installed
- ✅ **nuget CLI** on your PATH (for breaking change detection)
- ✅ An OpenAPI, AsyncAPI, or Protocol Buffer specification file

## Step 1: Install ConcordIO

Install the ConcordIO CLI tool globally:

```bash
dotnet tool install --global ConcordIO.Tool
```

Or as a local tool in your project:

```bash
dotnet new tool-manifest
dotnet tool install ConcordIO.Tool
```

Verify the installation:

```bash
concordio --version
```

## Step 2: Create Your First Contract Package

Let's say you have an OpenAPI specification file `petstore.yaml`:

```bash
concordio pack \
  --spec petstore.yaml \
  --package-id Contoso.PetStore.Api \
  --version 1.0.0
```

This command:
1. ✅ Generates a contract package containing your OpenAPI spec
2. ✅ Generates a client package for automatic code generation
3. ✅ Creates `.nupkg` files ready to publish

Output:
```
✓ Contract package: Contoso.PetStore.Api.1.0.0.nupkg
✓ Client package: Contoso.PetStore.Api.Client.1.0.0.nupkg
```

## Step 3: Publish to NuGet

Publish your packages to a NuGet feed:

```bash
# Publish to NuGet.org
dotnet nuget push Contoso.PetStore.Api.1.0.0.nupkg --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY

# Or to a private feed
dotnet nuget push *.nupkg --source https://your-feed.com/nuget
```

## Step 4: Consume the Contract in Another Project

In a consuming project, add the client package reference:

```xml
<ItemGroup>
  <PackageReference Include="Contoso.PetStore.Api.Client" Version="1.0.0" />
</ItemGroup>
```

Build your project:

```bash
dotnet build
```

The strongly-typed client is generated automatically! Use it in your code:

```csharp
using Contoso.PetStore.Api;

var client = new PetStoreClient(httpClient);
var pets = await client.GetPetsAsync();
```

## Step 5: Detect Breaking Changes

Before releasing a new version, check for breaking changes:

```bash
concordio breaking \
  --spec petstore-v2.yaml \
  --package-id Contoso.PetStore.Api \
  --version 1.0.0
```

Exit code:
- `0` = No breaking changes
- `1` = Breaking changes detected

## Next Steps

🎉 **Congratulations!** You've successfully:
- Installed ConcordIO
- Created and published a contract package
- Consumed the contract with automatic client generation
- Detected breaking changes

### Learn More

- [📦 Installation Guide](./installation.md) - Detailed installation options
- [🎯 When to Use ConcordIO](./when-to-use.md) - Understanding use cases
- [🏗️ Core Concepts](./concepts.md) - Deep dive into concepts
- [🛠️ CLI Tool Guide](../../src/ConcordIO.Tool/README.md) - Complete CLI reference
- [📝 Publishing Tutorial](../tutorials/publishing-first-contract.md) - Detailed walkthrough

## Common Commands Cheat Sheet

```bash
# Generate packages (without packing)
concordio generate --spec api.yaml --package-id My.Api --version 1.0.0

# Pack into .nupkg files
concordio pack --spec api.yaml --package-id My.Api --version 1.0.0

# Check for breaking changes
concordio breaking --spec api.yaml --package-id My.Api

# Get published spec
concordio get-spec --package-id My.Api --output-path api.yaml

# AsyncAPI
concordio pack --spec events.yaml:asyncapi --package-id My.Events --version 1.0.0

# Protocol Buffers
concordio pack --spec service.proto:proto --package-id My.Grpc --version 1.0.0 --client false

# Multiple specs
concordio pack \
  --spec api.yaml:openapi \
  --spec events.yaml:asyncapi \
  --package-id My.Service \
  --version 1.0.0
```

## Troubleshooting

### "concordio: command not found"

Make sure .NET tools are on your PATH:

```bash
export PATH="$PATH:$HOME/.dotnet/tools"  # Linux/macOS
# OR
set PATH=%PATH%;%USERPROFILE%\.dotnet\tools  # Windows
```

### "nuget: command not found"

Install the NuGet CLI:

```bash
# Windows (via Chocolatey)
choco install nuget.commandline

# macOS (via Homebrew)
brew install nuget

# Linux (via wget)
wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe
```

### Missing generated client types

Ensure you're referencing the `.Client` package, not the contract package:

```xml
<!-- ✅ Correct -->
<PackageReference Include="Contoso.PetStore.Api.Client" Version="1.0.0" />

<!-- ❌ Wrong (this only gives you the spec files) -->
<PackageReference Include="Contoso.PetStore.Api" Version="1.0.0" />
```

For multi-target projects (`<TargetFrameworks>`), use a single target instead:

```xml
<!-- Instead of <TargetFrameworks>net8.0;net9.0</TargetFrameworks> -->
<TargetFramework>net8.0</TargetFramework>
```

See [Known Limitations](../troubleshooting/known-limitations.md#openapi-multi-tfm) for details.

## Getting Help

- [❓ FAQ](../troubleshooting/faq.md)
- [🐛 Common Issues](../troubleshooting/common-issues.md)
- [GitHub Issues](https://github.com/LevDevIO/ConcordIO/issues)
