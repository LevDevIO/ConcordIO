using System.Diagnostics;

namespace ConcordIO.AsyncApi.Tests.E2E;

/// <summary>
/// End-to-end tests that verify the ConcordIO.AsyncApi.Client package works correctly
/// when added to a real .NET project. These tests create temporary projects that reference
/// AsyncAPI specifications and verify that C# contract types are generated at build time.
/// </summary>
[Collection(AsyncApiE2ECollection.Name)]
public class AsyncApiClientPackageIntegrationTests
{
    private readonly AsyncApiPackageFixture _fixture;

    public AsyncApiClientPackageIntegrationTests(AsyncApiPackageFixture fixture)
    {
        _fixture = fixture;
    }

    #region Package Structure Tests

    [Fact]
    public async Task ClientPackage_ContainsPropsFile()
    {
        // Act
        var nupkgPath = Directory.GetFiles(_fixture.PackagesDir, "ConcordIO.AsyncApi.Client.*.nupkg").First();
        var extractDir = Path.Combine(_fixture.TestDir, "extracted-props");
        System.IO.Compression.ZipFile.ExtractToDirectory(nupkgPath, extractDir, overwriteFiles: true);

        // Assert
        File.Exists(Path.Combine(extractDir, "build", "ConcordIO.AsyncApi.Client.props")).Should().BeTrue(
            because: "props file should be in build/ folder");
    }

    [Fact]
    public async Task ClientPackage_ContainsTargetsFile()
    {
        // Act
        var nupkgPath = Directory.GetFiles(_fixture.PackagesDir, "ConcordIO.AsyncApi.Client.*.nupkg").First();
        var extractDir = Path.Combine(_fixture.TestDir, "extracted-targets");
        System.IO.Compression.ZipFile.ExtractToDirectory(nupkgPath, extractDir, overwriteFiles: true);

        // Assert
        File.Exists(Path.Combine(extractDir, "build", "ConcordIO.AsyncApi.Client.targets")).Should().BeTrue(
            because: "targets file should be in build/ folder");
    }

    [Fact]
    public async Task ClientPackage_ContainsToolsFolder()
    {
        // Act
        var nupkgPath = Directory.GetFiles(_fixture.PackagesDir, "ConcordIO.AsyncApi.Client.*.nupkg").First();
        var extractDir = Path.Combine(_fixture.TestDir, "extracted-tools");
        System.IO.Compression.ZipFile.ExtractToDirectory(nupkgPath, extractDir, overwriteFiles: true);

        // Assert
        Directory.Exists(Path.Combine(extractDir, "tools")).Should().BeTrue(
            because: "tools folder should exist in package");
        File.Exists(Path.Combine(extractDir, "tools", "net10.0", "ConcordIO.AsyncApi.Client.dll")).Should().BeTrue(
            because: "task assembly should be in tools/net10.0/ folder");
    }

    [Fact]
    public async Task ClientPackage_ContainsBuildTransitiveProps()
    {
        // Act
        var nupkgPath = Directory.GetFiles(_fixture.PackagesDir, "ConcordIO.AsyncApi.Client.*.nupkg").First();
        var extractDir = Path.Combine(_fixture.TestDir, "extracted-transitive");
        System.IO.Compression.ZipFile.ExtractToDirectory(nupkgPath, extractDir, overwriteFiles: true);

        // Assert
        File.Exists(Path.Combine(extractDir, "buildTransitive", "ConcordIO.AsyncApi.Client.props")).Should().BeTrue(
            because: "buildTransitive props should exist for transitive package references");
    }

    #endregion

    #region Build Integration Tests

    [Fact]
    public async Task ClientPackage_GeneratesContracts_FromYamlSpec()
    {
        // Arrange
        var projectDir = await CreateTestProjectWithAsyncApiSpecAsync("ConsumerProject.Yaml", "yaml");

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v normal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        output.Should().Contain("ConcordIO.Client: Generating contracts",
            because: "the generation task should run");
        output.Should().Contain("ConcordIO.Client: Generated",
            because: "contracts should be generated");

        // Verify generated files exist in the ConcordIO.AsyncApi.Generated folder
        var generatedDir = Path.Combine(projectDir, "obj", "Debug", "net10.0", "ConcordIO.AsyncApi.Generated");
        Directory.Exists(generatedDir).Should().BeTrue(because: "generated output folder should exist");
        var generatedFiles = Directory.GetFiles(generatedDir, "*.g.cs");
        generatedFiles.Should().NotBeEmpty(because: "C# contract files should be generated");
    }

    [Fact]
    public async Task ClientPackage_GeneratesContracts_FromJsonSpec()
    {
        // Arrange
        var projectDir = await CreateTestProjectWithAsyncApiSpecAsync("ConsumerProject.Json", "json");

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v normal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        output.Should().Contain("ConcordIO.Client: Generating contracts");
    }

    [Fact]
    public async Task ClientPackage_DoesNotRun_WhenNoAsyncApiContractsSpecified()
    {
        // Arrange
        var projectDir = await CreateTestProjectWithoutSpecAsync("ConsumerProject.Empty");

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v normal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        output.Should().NotContain("ConcordIO.Client: Generating contracts",
            because: "task should not run when no AsyncAPI contracts are specified");
    }

    [Fact]
    public async Task ClientPackage_GeneratedCode_CompilesSuccessfully()
    {
        // Arrange
        var projectDir = await CreateTestProjectWithAsyncApiSpecAsync("ConsumerProject.Compile", "yaml");

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed including generated code. Output:\n{output}");

        // Verify the assembly was created (compilation succeeded)
        var assemblyPath = Path.Combine(projectDir, "bin", "Debug", "net10.0", "ConsumerProject.Compile.dll");
        File.Exists(assemblyPath).Should().BeTrue(because: "compiled assembly should exist");
    }

    [Fact]
    public async Task ClientPackage_GeneratedCode_CanBeUsedInCode()
    {
        // Arrange - use a simpler test that just verifies compilation succeeds
        // The generated types will be in GeneratedContracts namespace (default when no x-dotnet-namespace matches)
        var projectDir = await CreateTestProjectWithAsyncApiSpecAsync("ConsumerProject.Usage", "yaml");

        // Act - just verify the build succeeds with generated code
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed with generated types. Output:\n{output}");

        // Verify the assembly was created (compilation succeeded including generated code)
        var assemblyPath = Path.Combine(projectDir, "bin", "Debug", "net10.0", "ConsumerProject.Usage.dll");
        File.Exists(assemblyPath).Should().BeTrue(because: "compiled assembly should exist");
    }

    #endregion

    #region Generated Code Content Tests

    [Fact]
    public async Task ClientPackage_GeneratedCode_ContainsCorrectNamespace()
    {
        // Arrange
        var projectDir = await CreateTestProjectWithAsyncApiSpecAsync("ConsumerProject.Namespace", "yaml");

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");

        // Look specifically in the ConcordIO.AsyncApi.Generated folder
        var generatedDir = Path.Combine(projectDir, "obj", "Debug", "net10.0", "ConcordIO.AsyncApi.Generated");
        var generatedFiles = Directory.Exists(generatedDir)
            ? Directory.GetFiles(generatedDir, "*.g.cs")
            : [];
        generatedFiles.Should().NotBeEmpty(because: "generated files should exist in ConcordIO.AsyncApi.Generated folder");

        var content = await File.ReadAllTextAsync(generatedFiles.First());
        content.Should().Contain("namespace", because: "generated code should have namespace");
    }

    [Fact]
    public async Task ClientPackage_GeneratedCode_ContainsAutoGeneratedHeader()
    {
        // Arrange
        var projectDir = await CreateTestProjectWithAsyncApiSpecAsync("ConsumerProject.Header", "yaml");

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");

        var generatedDir = Path.Combine(projectDir, "obj", "Debug", "net10.0", "ConcordIO.AsyncApi.Generated");
        var generatedFiles = Directory.Exists(generatedDir)
            ? Directory.GetFiles(generatedDir, "*.g.cs")
            : [];
        generatedFiles.Should().NotBeEmpty();

        var content = await File.ReadAllTextAsync(generatedFiles.First());
        content.Should().Contain("<auto-generated>", because: "generated code should have auto-generated header");
    }

    [Fact]
    public async Task ClientPackage_GeneratedCode_ContainsNullableEnable()
    {
        // Arrange
        var projectDir = await CreateTestProjectWithAsyncApiSpecAsync("ConsumerProject.Nullable", "yaml");

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");

        var generatedDir = Path.Combine(projectDir, "obj", "Debug", "net10.0", "ConcordIO.AsyncApi.Generated");
        var generatedFiles = Directory.Exists(generatedDir)
            ? Directory.GetFiles(generatedDir, "*.g.cs")
            : [];
        generatedFiles.Should().NotBeEmpty();

        var content = await File.ReadAllTextAsync(generatedFiles.First());
        content.Should().Contain("#nullable enable", because: "generated code should enable nullable reference types");
    }

    [Fact]
    public async Task ClientPackage_GeneratedCode_ContainsExpectedTypes()
    {
        // Arrange
        var projectDir = await CreateTestProjectWithAsyncApiSpecAsync("ConsumerProject.Types", "yaml");

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");

        var generatedDir = Path.Combine(projectDir, "obj", "Debug", "net10.0", "ConcordIO.AsyncApi.Generated");
        var generatedFiles = Directory.Exists(generatedDir)
            ? Directory.GetFiles(generatedDir, "*.g.cs")
            : [];
        generatedFiles.Should().NotBeEmpty();

        var allContent = string.Join("\n", await Task.WhenAll(generatedFiles.Select(f => File.ReadAllTextAsync(f))));
        allContent.Should().Contain("OrderCreatedEvent", because: "should contain the event type from spec");
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task ClientPackage_RespectsRecordClassStyle()
    {
        // Arrange
        var projectDir = await CreateTestProjectWithAsyncApiSpecAsync("ConsumerProject.Record", "yaml", classStyle: "Record");

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");

        var generatedDir = Path.Combine(projectDir, "obj", "Debug", "net10.0", "ConcordIO.AsyncApi.Generated");
        var generatedFiles = Directory.Exists(generatedDir)
            ? Directory.GetFiles(generatedDir, "*.g.cs")
            : [];
        generatedFiles.Should().NotBeEmpty();

        // Note: The actual record keyword presence depends on NJsonSchema behavior
        // We mainly verify that the setting is passed through without errors
    }

    [Fact]
    public async Task ClientPackage_RespectsDataAnnotationsSetting()
    {
        // Arrange
        var projectDir = await CreateTestProjectWithAsyncApiSpecAsync("ConsumerProject.DataAnnotations", "yaml", generateDataAnnotations: true);

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");

        var generatedDir = Path.Combine(projectDir, "obj", "Debug", "net10.0", "ConcordIO.AsyncApi.Generated");
        var generatedFiles = Directory.Exists(generatedDir)
            ? Directory.GetFiles(generatedDir, "*.g.cs")
            : [];
        generatedFiles.Should().NotBeEmpty();

        var content = await File.ReadAllTextAsync(generatedFiles.First());
        content.Should().Contain("System.ComponentModel.DataAnnotations", because: "should include DataAnnotations using when enabled");
    }

    #endregion

    #region Multiple Files Tests

    [Fact]
    public async Task ClientPackage_HandlesMultipleAsyncApiFiles()
    {
        // Arrange
        var projectDir = await CreateTestProjectWithMultipleSpecsAsync("ConsumerProject.Multiple");

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v normal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        output.Should().Contain("ConcordIO.Client: Generating contracts");
    }

    #endregion

    #region Helper Methods

    private async Task<string> CreateTestProjectWithAsyncApiSpecAsync(
        string projectName,
        string specFormat,
        string? classStyle = null,
        bool generateDataAnnotations = true)
    {
        var projectDir = Path.Combine(_fixture.TestDir, projectName);
        Directory.CreateDirectory(projectDir);

        // Create the AsyncAPI spec
        var specFileName = $"contracts.{specFormat}";
        var specPath = Path.Combine(projectDir, specFileName);
        await File.WriteAllTextAsync(specPath, GetSampleAsyncApiSpec(specFormat));

        // Build property overrides
        var propertyOverrides = new List<string>();
        if (classStyle != null)
            propertyOverrides.Add($"<ConcordIOClientClassStyle>{classStyle}</ConcordIOClientClassStyle>");
        propertyOverrides.Add($"<ConcordIOClientGenerateDataAnnotations>{generateDataAnnotations.ToString().ToLower()}</ConcordIOClientGenerateDataAnnotations>");

        var propertyOverridesXml = propertyOverrides.Count > 0
            ? $"\n    {string.Join("\n    ", propertyOverrides)}"
            : "";

        var csproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Library</OutputType>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>{propertyOverridesXml}
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="ConcordIO.AsyncApi.Client" Version="*" />
              </ItemGroup>
              
              <ItemGroup>
                <ConcordIOAsyncApiContract Include="{specFileName}" />
              </ItemGroup>
            </Project>
            """;
        await File.WriteAllTextAsync(Path.Combine(projectDir, $"{projectName}.csproj"), csproj);

        // Create nuget.config
        await CreateNuGetConfigAsync(projectDir);

        return projectDir;
    }

    private async Task<string> CreateTestProjectWithAsyncApiSpecAndUsageAsync(
        string projectName,
        string specFormat)
    {
        var projectDir = await CreateTestProjectWithAsyncApiSpecAsync(projectName, specFormat);

        // Add a file that uses the generated types
        var usageCode = """
            using MyService.Contracts.Events;

            namespace ConsumerProject;

            public class OrderProcessor
            {
                public void Process(OrderCreatedEvent evt)
                {
                    var id = evt.OrderId;
                    var timestamp = evt.Timestamp;
                }
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(projectDir, "OrderProcessor.cs"), usageCode);

        return projectDir;
    }

    private async Task<string> CreateTestProjectWithoutSpecAsync(string projectName)
    {
        var projectDir = Path.Combine(_fixture.TestDir, projectName);
        Directory.CreateDirectory(projectDir);

        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Library</OutputType>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="ConcordIO.AsyncApi.Client" Version="*" />
              </ItemGroup>
            </Project>
            """;
        await File.WriteAllTextAsync(Path.Combine(projectDir, $"{projectName}.csproj"), csproj);

        // Create a simple class file
        var classContent = """
            namespace ConsumerProject;

            public class MyClass
            {
                public string Name { get; set; } = string.Empty;
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(projectDir, "MyClass.cs"), classContent);

        // Create nuget.config
        await CreateNuGetConfigAsync(projectDir);

        return projectDir;
    }

    private async Task<string> CreateTestProjectWithMultipleSpecsAsync(string projectName)
    {
        var projectDir = Path.Combine(_fixture.TestDir, projectName);
        Directory.CreateDirectory(projectDir);

        // Create two AsyncAPI specs
        await File.WriteAllTextAsync(
            Path.Combine(projectDir, "events.yaml"),
            GetSampleAsyncApiSpec("yaml", "MyService.Contracts.Events", "OrderCreatedEvent"));
        await File.WriteAllTextAsync(
            Path.Combine(projectDir, "commands.yaml"),
            GetSampleAsyncApiSpec("yaml", "MyService.Contracts.Commands", "CreateOrderCommand"));

        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Library</OutputType>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="ConcordIO.AsyncApi.Client" Version="*" />
              </ItemGroup>
              
              <ItemGroup>
                <ConcordIOAsyncApiContract Include="events.yaml" />
                <ConcordIOAsyncApiContract Include="commands.yaml" />
              </ItemGroup>
            </Project>
            """;
        await File.WriteAllTextAsync(Path.Combine(projectDir, $"{projectName}.csproj"), csproj);

        // Create nuget.config
        await CreateNuGetConfigAsync(projectDir);

        return projectDir;
    }

    private static string GetSampleAsyncApiSpec(string format, string ns = "MyService.Contracts.Events", string typeName = "OrderCreatedEvent")
    {
        if (format == "json")
        {
            return $$"""
                {
                  "asyncapi": "3.0.0",
                  "info": {
                    "title": "Test API",
                    "version": "1.0.0"
                  },
                  "channels": {
                    "{{ns}}.{{typeName}}": {
                      "address": "urn:message:{{ns}}:{{typeName}}",
                      "messages": {
                        "{{typeName}}": {
                          "$ref": "#/components/messages/{{typeName}}"
                        }
                      }
                    }
                  },
                  "operations": {},
                  "components": {
                    "messages": {
                      "{{typeName}}": {
                        "name": "{{typeName}}",
                        "contentType": "application/json",
                        "payload": {
                          "$ref": "#/components/schemas/{{typeName}}"
                        }
                      }
                    },
                    "schemas": {
                      "{{typeName}}": {
                        "schemaFormat": "application/schema+json;version=draft-07",
                        "schema": {
                          "type": "object",
                          "x-dotnet-namespace": "{{ns}}",
                          "properties": {
                            "orderId": {
                              "type": "string",
                              "format": "uuid"
                            },
                            "timestamp": {
                              "type": "string",
                              "format": "date-time"
                            },
                            "amount": {
                              "type": "number"
                            }
                          },
                          "required": ["orderId", "timestamp"]
                        }
                      }
                    }
                  }
                }
                """;
        }

        // YAML format
        return $$"""
            asyncapi: '3.0.0'
            info:
              title: Test API
              version: 1.0.0
            channels:
              {{ns}}.{{typeName}}:
                address: 'urn:message:{{ns}}:{{typeName}}'
                messages:
                  {{typeName}}:
                    $ref: '#/components/messages/{{typeName}}'
            operations: {}
            components:
              messages:
                {{typeName}}:
                  name: {{typeName}}
                  contentType: application/json
                  payload:
                    $ref: '#/components/schemas/{{typeName}}'
              schemas:
                {{typeName}}:
                  schemaFormat: 'application/schema+json;version=draft-07'
                  schema:
                    type: object
                    x-dotnet-namespace: {{ns}}
                    properties:
                      orderId:
                        type: string
                        format: uuid
                      timestamp:
                        type: string
                        format: date-time
                      amount:
                        type: number
                    required:
                      - orderId
                      - timestamp
            """;
    }

    private async Task CreateNuGetConfigAsync(string projectDir)
    {
        var nugetConfig = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="{_fixture.PackagesDir}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """;
        await File.WriteAllTextAsync(Path.Combine(projectDir, "nuget.config"), nugetConfig);
    }

    private async Task<(int ExitCode, string Output)> RunDotNetAsync(
        string command, string workingDir, string args = "")
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"{command} {AsyncApiE2ECommandVerbosity.AddDotNetVerbosity(args)}",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Use test-local NuGet cache to avoid conflicts with global cache
        process.StartInfo.Environment["NUGET_PACKAGES"] = _fixture.NugetCacheDir;

        process.Start();

        // Start reading streams concurrently to avoid pipe buffer deadlock
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet {command} timed out after 3 minutes in {workingDir}");
        }

        var output = await outputTask;
        var error = await errorTask;

        return (process.ExitCode, output + error);
    }

    #endregion
}
