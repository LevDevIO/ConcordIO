# 🤖 AI Prompts for ConcordIO

Ready-to-use prompts for AI coding assistants (GitHub Copilot, ChatGPT, Claude, etc.) to help you work with ConcordIO more effectively.

## Table of Contents

- [Adding ConcordIO to Projects](#adding-concordio-to-projects)
- [Generating Contracts](#generating-contracts)
- [Consuming Contracts](#consuming-contracts)
- [Troubleshooting](#troubleshooting)
- [CI/CD Integration](#cicd-integration)
- [Code Reviews](#code-reviews)

## Adding ConcordIO to Projects

### Prompt: Add ConcordIO CLI Tool

```
Add the ConcordIO.Tool as a local dotnet tool to this project. Create or update the tool manifest file and install the tool.
```

**What the AI should do**:
1. Create `.config/dotnet-tools.json` if it doesn't exist
2. Add ConcordIO.Tool entry with latest version
3. Provide command to restore tools

### Prompt: Add AsyncAPI Server Package

```
Add the ConcordIO.AsyncApi.Server package to this project. Configure it to:
- Generate AsyncAPI spec from types in the MyApp.Contracts.Events namespace
- Generate AsyncAPI spec from types in the MyApp.Contracts.Commands namespace
- Use JSON output format
- Include the spec in the NuGet package
```

**What the AI should do**:
1. Add `<PackageReference>` with correct attributes
2. Add MSBuild properties for type patterns
3. Configure output format

### Prompt: Add AsyncAPI Client Package

```
Add the ConcordIO.AsyncApi.Client package to this project. Configure it to:
- Generate types as records instead of POCOs
- Disable data annotations
- Output to a custom directory under src/Generated
```

**What the AI should do**:
1. Add `<PackageReference>` with correct attributes
2. Configure MSBuild properties
3. Optionally update .gitignore

## Generating Contracts

### Prompt: Create Contract Package from OpenAPI

```
I have an OpenAPI spec at specs/api.yaml. Use ConcordIO to:
1. Generate contract and client NuGet packages
2. Package ID should be "Contoso.PetStore.Api"
3. Version should be "1.0.0"
4. Output to ./packages directory
5. Include custom NSwag option: GenerateClientInterfaces=true
6. Add package property: RepositoryUrl=https://github.com/contoso/petstore

Show me the complete command.
```

**Expected output**:
```bash
concordio pack \
  --spec specs/api.yaml \
  --package-id Contoso.PetStore.Api \
  --version 1.0.0 \
  --output ./packages \
  --nswag-options GenerateClientInterfaces=true \
  --package-properties RepositoryUrl=https://github.com/contoso/petstore
```

### Prompt: Create Multi-Spec Package

```
I have multiple specs:
- OpenAPI: specs/rest-api.yaml
- AsyncAPI: specs/events.yaml
- Proto: specs/grpc.proto

Create a single contract package that includes all three. Package ID should be "MyCompany.MyService.Contracts", version "2.0.0". Don't generate a client package (I'll handle that separately).
```

**Expected output**:
```bash
concordio pack \
  --spec specs/rest-api.yaml:openapi \
  --spec specs/events.yaml:asyncapi \
  --spec specs/grpc.proto:proto \
  --package-id MyCompany.MyService.Contracts \
  --version 2.0.0 \
  --client false
```

### Prompt: Generate Package Structure Only

```
I want to see what files ConcordIO will generate before packing. Use the generate command (not pack) for my OpenAPI spec at api.yaml, package ID "Test.Api", version "1.0.0".
```

**Expected output**:
```bash
concordio generate \
  --spec api.yaml \
  --package-id Test.Api \
  --version 1.0.0
```

## Consuming Contracts

### Prompt: Add Contract Package Reference

```
Add a reference to the contract client package "Contoso.PetStore.Api.Client" version "1.0.0" to my .NET project. Make sure it's configured as a development dependency.
```

**What the AI should do**:
```xml
<ItemGroup>
  <PackageReference Include="Contoso.PetStore.Api.Client" Version="1.0.0" />
</ItemGroup>
```

Note: Development dependency is already configured in the client package itself.

### Prompt: Customize OpenAPI Client Generation

```
I'm consuming an OpenAPI contract package. I want to customize the generated client to:
- Use a custom namespace: MyApp.ExternalClients.PetStore
- Enable nullable reference types
- Use Newtonsoft.Json instead of System.Text.Json
- Inject HttpClient via dependency injection

Add a custom MSBuild target that applies these settings after ConcordIO's target runs.
```

**What the AI should do**:
```xml
<Target Name="CustomizeOpenApiClient" AfterTargets="ConcordIOAddOpenApiReferenceForNSwag">
  <ItemGroup>
    <OpenApiReference Update="@(OpenApiReference)">
      <Namespace>MyApp.ExternalClients.PetStore</Namespace>
      <NSwagGenerateNullableReferenceTypes>true</NSwagGenerateNullableReferenceTypes>
      <NSwagJsonLibrary>NewtonsoftJson</NSwagJsonLibrary>
      <NSwagInjectHttpClient>true</NSwagInjectHttpClient>
    </OpenApiReference>
  </ItemGroup>
</Target>
```

### Prompt: Use Generated Client

```
I've added the Contoso.PetStore.Api.Client package to my project. Show me how to:
1. Register the client in dependency injection
2. Use it in a controller to get a list of pets
3. Handle errors properly
```

**What the AI should do**:
```csharp
// In Program.cs or Startup.cs
services.AddHttpClient<PetStoreClient>(client =>
{
    client.BaseAddress = new Uri("https://api.petstore.com");
});

// In a controller
public class PetsController : ControllerBase
{
    private readonly PetStoreClient _client;

    public PetsController(PetStoreClient client)
    {
        _client = client;
    }

    [HttpGet]
    public async Task<IActionResult> GetPets()
    {
        try
        {
            var pets = await _client.GetPetsAsync();
            return Ok(pets);
        }
        catch (ApiException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }
}
```

## Troubleshooting

### Prompt: Debug Missing Generated Code

```
I've added the ConcordIO client package but the generated client types aren't available at compile time. Help me debug this issue. My project uses <TargetFrameworks>net8.0;net9.0</TargetFrameworks>.
```

**What the AI should do**:
1. Identify multi-TFM as potential issue
2. Suggest changing to single `<TargetFramework>`
3. Provide diagnostic commands:
```bash
dotnet build -v n  # Check for generation messages
dotnet build -v diag > build.log  # Detailed logging
```
4. Point to known limitations documentation

### Prompt: Fix AsyncAPI Generation Issues

```
My AsyncAPI client generation isn't running. I have ConcordIO.AsyncApi.Client installed and a contract package referenced. Help me diagnose the issue.
```

**What the AI should do**:
1. Verify `@(ConcordIOAsyncApiContract)` items exist:
```bash
dotnet build -v n | grep ConcordIOAsyncApiContract
```
2. Check package references
3. Suggest verbose build logging
4. Point to troubleshooting docs

### Prompt: Understand Breaking Changes Report

```
I ran `concordio breaking` and got exit code 1. Explain what this means and what I should do next.
```

**What the AI should do**:
1. Explain exit code 1 = breaking changes detected
2. Suggest reviewing the output for specific changes
3. Recommend version bump strategy (major version)
4. Provide example of how to proceed

## CI/CD Integration

### Prompt: Add Breaking Change Check to GitHub Actions

```
Add a GitHub Actions workflow that:
1. Runs on pull requests
2. Checks for breaking changes in specs/api.yaml
3. Compares against the latest published version of "MyCompany.Api" package
4. Fails the PR if breaking changes are detected
5. Adds a comment to the PR with the results
```

**What the AI should do**:
```yaml
name: Check Breaking Changes

on:
  pull_request:
    paths:
      - 'specs/api.yaml'

jobs:
  breaking-changes:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Install ConcordIO
        run: dotnet tool install --global ConcordIO.Tool
      
      - name: Check for breaking changes
        id: breaking
        run: |
          concordio breaking \
            --spec specs/api.yaml \
            --package-id MyCompany.Api \
            > breaking-output.txt 2>&1
          echo "exit_code=$?" >> $GITHUB_OUTPUT
        continue-on-error: true
      
      - name: Comment on PR
        uses: actions/github-script@v6
        with:
          script: |
            const fs = require('fs');
            const output = fs.readFileSync('breaking-output.txt', 'utf8');
            const exitCode = '${{ steps.breaking.outputs.exit_code }}';
            
            const comment = exitCode === '1' 
              ? `⚠️ **Breaking changes detected!**\n\n\`\`\`\n${output}\n\`\`\``
              : `✅ No breaking changes detected.`;
            
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: comment
            });
      
      - name: Fail if breaking changes
        if: steps.breaking.outputs.exit_code == '1'
        run: exit 1
```

### Prompt: Add Auto-Publish Workflow

```
Create a GitHub Actions workflow that:
1. Triggers on push to main branch
2. Generates and packs contract packages from specs/*
3. Determines version from git tags
4. Publishes to GitHub Packages
5. Only runs if spec files changed
```

**What the AI should do**: Provide a complete workflow file with versioning strategy

## Code Reviews

### Prompt: Review Contract Package Setup

```
Review my ConcordIO setup in this project. Check for:
- Correct package references and versions
- MSBuild property configuration
- Potential issues with code generation
- Missing or incorrect settings
- Best practices

Here's my .csproj file:
[paste csproj content]
```

**What the AI should do**:
1. Verify package references have correct attributes
2. Check MSBuild properties are valid
3. Suggest improvements
4. Identify potential issues

### Prompt: Review Breaking Changes Process

```
Review my breaking change detection strategy. I want to ensure:
- Breaking changes are caught before merge
- Version bumps follow SemVer correctly
- CI/CD enforces the policy

Here's my current setup:
[paste workflow/process details]
```

**What the AI should do**:
1. Evaluate the breaking change detection approach
2. Check version bumping strategy
3. Suggest improvements
4. Identify gaps in the process

## Advanced Prompts

### Prompt: Create Custom Code Generator

```
I want to create a custom MSBuild task that generates code from my ConcordIO contract packages, similar to how the AsyncAPI client works. Guide me through:
1. Creating the task project
2. Implementing the task
3. Packaging it for NuGet
4. Wiring it to ConcordIOContract items
```

### Prompt: Migrate Existing Project

```
I have an existing project that uses NSwag with local OpenAPI files. Help me migrate to ConcordIO:
1. Create contract packages from existing specs
2. Update project references
3. Replace local OpenApiReference with package references
4. Ensure generated code stays the same
5. Provide a migration checklist
```

### Prompt: Multi-Repository Setup

```
I have a microservices architecture with:
- 1 contracts repository (specs only)
- 5 service repositories (consume contracts)

Design a ConcordIO setup that:
- Publishes contracts from the contracts repo
- Services automatically get updates
- Breaking changes block deployments
- Version management is automated
```

## Tips for Using These Prompts

1. **Be Specific**: Include file paths, package names, versions
2. **Provide Context**: Share your .csproj, specs, or error messages
3. **Ask for Explanations**: Request "explain why" after solutions
4. **Iterate**: Refine prompts based on AI responses
5. **Verify**: Always review AI-generated code before using

## Next Steps

- [📝 Tutorial: Publishing Your First Contract](../tutorials/publishing-first-contract.md)
- [🔄 Tutorial: Consuming a Contract Package](../tutorials/consuming-contract.md)
- [🚦 Tutorial: CI/CD Setup](../tutorials/cicd-setup.md)
- [❓ FAQ](../troubleshooting/faq.md)

## Contributing Prompts

Have a useful prompt? [Contribute it to the documentation](../../CONTRIBUTING.md)!
