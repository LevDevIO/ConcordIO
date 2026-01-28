using System.Diagnostics;

namespace ConcordIO.AsyncApi.Tests.E2E;

/// <summary>
/// Fixture that builds and packs the server project once for all E2E tests.
/// Using IAsyncLifetime for proper async initialization without deadlocks.
/// </summary>
public class ServerPackageFixture : IAsyncLifetime
{
    public string TestDir { get; private set; } = null!;
    public string PackagesDir { get; private set; } = null!;
    public string NugetCacheDir { get; private set; } = null!;
    public string ServerProjectPath { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        TestDir = Path.Combine(Path.GetTempPath(), "ConcordIO.AsyncApi.Tests", Path.GetRandomFileName().Replace(".", ""));
        PackagesDir = Path.Combine(TestDir, "packages");
        NugetCacheDir = Path.Combine(TestDir, "nuget-cache");
        Directory.CreateDirectory(TestDir);
        Directory.CreateDirectory(PackagesDir);
        Directory.CreateDirectory(NugetCacheDir);

        // Find the server project path relative to the test assembly
        var testAssemblyDir = Path.GetDirectoryName(typeof(ServerPackageFixture).Assembly.Location)!;
        ServerProjectPath = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", "..", "ConcordIO.AsyncApi.Server", "ConcordIO.AsyncApi.Server.csproj"));

        // Build and pack the server project
        var (buildExitCode, buildOutput) = await RunDotNetAsync("build", Path.GetDirectoryName(ServerProjectPath)!, "-c Release");
        if (buildExitCode != 0)
            throw new Exception($"Server project build failed: {buildOutput}");

        var (packExitCode, packOutput) = await RunDotNetAsync("pack", Path.GetDirectoryName(ServerProjectPath)!,
            $"-c Release -o \"{PackagesDir}\"");
        if (packExitCode != 0)
            throw new Exception($"Server project pack failed: {packOutput}");
    }

    public Task DisposeAsync()
    {
        try
        {
            Directory.Delete(TestDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors
        }
        return Task.CompletedTask;
    }

    private async Task<(int ExitCode, string Output)> RunDotNetAsync(string command, string workingDir, string args = "")
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"{command} {args}",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.StartInfo.Environment["NUGET_PACKAGES"] = NugetCacheDir;

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output + error);
    }
}

/// <summary>
/// End-to-end tests that verify the ConcordIO.AsyncApi.Server package works correctly
/// when added to a real .NET project. These tests create temporary projects, add the
/// package reference, and verify that AsyncAPI specs are generated at build time.
/// </summary>
public class AsyncApiServerPackageIntegrationTests : IClassFixture<ServerPackageFixture>
{
    private readonly ServerPackageFixture _fixture;

    public AsyncApiServerPackageIntegrationTests(ServerPackageFixture fixture)
    {
        _fixture = fixture;
    }

    #region Package Structure Tests

    [Fact]
    public async Task ServerPackage_ContainsPropsFile()
    {
        // Act
        var nupkgPath = Directory.GetFiles(_fixture.PackagesDir, "ConcordIO.AsyncApi.Server.*.nupkg").First();
        var extractDir = Path.Combine(_fixture.TestDir, "extracted-props");
        System.IO.Compression.ZipFile.ExtractToDirectory(nupkgPath, extractDir);

        // Assert
        File.Exists(Path.Combine(extractDir, "build", "ConcordIO.AsyncApi.Server.props")).Should().BeTrue(
            because: "props file should be in build/ folder");
    }

    [Fact]
    public async Task ServerPackage_ContainsTargetsFile()
    {
        // Act
        var nupkgPath = Directory.GetFiles(_fixture.PackagesDir, "ConcordIO.AsyncApi.Server.*.nupkg").First();
        var extractDir = Path.Combine(_fixture.TestDir, "extracted-targets");
        System.IO.Compression.ZipFile.ExtractToDirectory(nupkgPath, extractDir);

        // Assert
        File.Exists(Path.Combine(extractDir, "build", "ConcordIO.AsyncApi.Server.targets")).Should().BeTrue(
            because: "targets file should be in build/ folder");
    }

    [Fact]
    public async Task ServerPackage_ContainsToolsFolder()
    {
        // Act
        var nupkgPath = Directory.GetFiles(_fixture.PackagesDir, "ConcordIO.AsyncApi.Server.*.nupkg").First();
        var extractDir = Path.Combine(_fixture.TestDir, "extracted-tools");
        System.IO.Compression.ZipFile.ExtractToDirectory(nupkgPath, extractDir);

        // Assert
        Directory.Exists(Path.Combine(extractDir, "tools")).Should().BeTrue(
            because: "tools folder should exist in package");
        File.Exists(Path.Combine(extractDir, "tools", "net10.0", "ConcordIO.AsyncApi.Server.dll")).Should().BeTrue(
            because: "task assembly should be in tools/net10.0/ folder");
    }

    [Fact]
    public async Task ServerPackage_ContainsBuildTransitiveProps()
    {
        // Act
        var nupkgPath = Directory.GetFiles(_fixture.PackagesDir, "ConcordIO.AsyncApi.Server.*.nupkg").First();
        var extractDir = Path.Combine(_fixture.TestDir, "extracted-transitive");
        System.IO.Compression.ZipFile.ExtractToDirectory(nupkgPath, extractDir);

        // Assert
        File.Exists(Path.Combine(extractDir, "buildTransitive", "ConcordIO.AsyncApi.Server.props")).Should().BeTrue(
            because: "buildTransitive props should exist for transitive package references");
    }

    #endregion

    #region Build Integration Tests

    [Fact]
    public async Task ServerPackage_GeneratesAsyncApiSpec_WhenEventTypesSpecified()
    {
        var projectDir = await CreateTestProjectWithMessageTypesAsync("TestContracts.Events",
            eventTypes: ["TestContracts.Events.**"]);

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v normal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        output.Should().Contain("ConcordIO: Generating AsyncAPI specification",
            because: "the generation task should run");
        output.Should().Contain("ConcordIO: Generated",
            because: "the spec should be generated");

        // Verify the spec file exists
        var specFile = Directory.GetFiles(projectDir, "*.yaml", SearchOption.AllDirectories)
            .FirstOrDefault(f => f.Contains("asyncapi"));
        specFile.Should().NotBeNull(because: "AsyncAPI spec file should be generated");
    }

    [Fact]
    public async Task ServerPackage_GeneratesAsyncApiSpec_WhenCommandTypesSpecified()
    {
        var projectDir = await CreateTestProjectWithMessageTypesAsync("TestContracts.Commands",
            commandTypes: ["TestContracts.Commands.**"]);

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v normal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        output.Should().Contain("ConcordIO: Generating AsyncAPI specification");
    }

    [Fact]
    public async Task ServerPackage_GeneratesAsyncApiSpec_WithMixedEventAndCommandTypes()
    {
        var projectDir = await CreateTestProjectWithMessageTypesAsync("TestContracts.Mixed",
            eventTypes: ["TestContracts.Mixed.Events.**"],
            commandTypes: ["TestContracts.Mixed.Commands.**"],
            includeEventsAndCommands: true);

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v normal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        output.Should().Contain("ConcordIO: Generating AsyncAPI specification");
    }

    [Fact]
    public async Task ServerPackage_GeneratesAsyncApiSpec_WithSingleWildcard()
    {
        var projectDir = await CreateTestProjectWithMessageTypesAsync("TestContracts.SingleWildcard",
            eventTypes: ["TestContracts.SingleWildcard.Events.*"],  // Single wildcard - exact namespace
            commandTypes: ["TestContracts.SingleWildcard.Commands.*"],
            includeEventsAndCommands: true);

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v normal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        output.Should().Contain("ConcordIO: Generating AsyncAPI specification",
            because: "single wildcard .* should find types in the exact namespace");
        output.Should().Contain("Found 4 message types",
            because: "should find 2 events + 2 commands (OrderItem is in Commands namespace)");
    }

    [Fact]
    public async Task ServerPackage_DoesNotRun_WhenNoMessageTypesSpecified()
    {
        var projectDir = await CreateTestProjectWithMessageTypesAsync("TestContracts.Empty");

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v normal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        output.Should().NotContain("ConcordIO: Generating AsyncAPI specification",
            because: "task should not run when no message types are specified");
    }

    [Fact]
    public async Task ServerPackage_GeneratesYamlByDefault()
    {
        var projectDir = await CreateTestProjectWithMessageTypesAsync("TestContracts.Yaml",
            eventTypes: ["TestContracts.Yaml.**"]);

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        var specFiles = Directory.GetFiles(projectDir, "*.yaml", SearchOption.AllDirectories);
        specFiles.Should().Contain(f => f.Contains("asyncapi") || f.Contains("TestContracts"),
            because: "YAML spec file should be generated by default");
    }

    [Fact]
    public async Task ServerPackage_GeneratesJson_WhenFormatSpecified()
    {
        var projectDir = await CreateTestProjectWithMessageTypesAsync("TestContracts.Json",
            eventTypes: ["TestContracts.Json.**"],
            outputFormat: "json");

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        var specFiles = Directory.GetFiles(projectDir, "*.json", SearchOption.AllDirectories);
        specFiles.Should().Contain(f => f.Contains("asyncapi") || f.Contains("TestContracts"),
            because: "JSON spec file should be generated when format is json");
    }

    #endregion

    #region Document Content Tests

    [Fact]
    public async Task ServerPackage_GeneratedSpec_ContainsCorrectAsyncApiVersion()
    {
        var projectDir = await CreateTestProjectWithMessageTypesAsync("TestContracts.Version",
            eventTypes: ["TestContracts.Version.**"],
            includeEventsAndCommands: true);

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        var specFiles = Directory.GetFiles(projectDir, "*.yaml", SearchOption.AllDirectories)
            .Where(f => f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .ToArray();
        specFiles.Should().NotBeEmpty(because: "AsyncAPI spec file should be generated in obj folder");
        var content = await File.ReadAllTextAsync(specFiles.First());
        content.Should().Contain("asyncapi:", because: "should contain AsyncAPI marker");
    }

    [Fact]
    public async Task ServerPackage_GeneratedSpec_ContainsDocumentTitle()
    {
        var projectDir = await CreateTestProjectWithMessageTypesAsync("MyService.Contracts",
            eventTypes: ["MyService.Contracts.**"],
            includeEventsAndCommands: true);

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        var specFiles = Directory.GetFiles(projectDir, "*.yaml", SearchOption.AllDirectories)
            .Where(f => f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .ToArray();
        specFiles.Should().NotBeEmpty(because: "AsyncAPI spec file should be generated in obj folder");
        var content = await File.ReadAllTextAsync(specFiles.First());
        content.Should().Contain("title:", because: "should contain title");
        content.Should().Contain("MyService.Contracts", because: "title should be the assembly name");
    }

    [Fact]
    public async Task ServerPackage_GeneratedSpec_ContainsDiscoveredTypes()
    {
        var projectDir = await CreateTestProjectWithMessageTypesAsync("TestContracts.Discovery",
            eventTypes: ["TestContracts.Discovery.**"],
            includeEventsAndCommands: true);

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        var specFiles = Directory.GetFiles(projectDir, "*.yaml", SearchOption.AllDirectories)
            .Where(f => f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .ToArray();
        specFiles.Should().NotBeEmpty(because: "AsyncAPI spec file should be generated in obj folder");
        var content = await File.ReadAllTextAsync(specFiles.First());
        content.Should().Contain("OrderCreatedEvent", because: "should contain the discovered event type");
        content.Should().Contain("channels:", because: "should contain channels section");
        content.Should().Contain("components:", because: "should contain components section");
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task ServerPackage_UsesCustomDocumentVersion_WhenSpecified()
    {
        var projectDir = await CreateTestProjectWithMessageTypesAsync("TestContracts.CustomVersion",
            eventTypes: ["TestContracts.CustomVersion.**"],
            documentVersion: "2.5.0",
            includeEventsAndCommands: true);

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        var specFiles = Directory.GetFiles(projectDir, "*.yaml", SearchOption.AllDirectories)
            .Where(f => f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .ToArray();
        specFiles.Should().NotBeEmpty(because: "AsyncAPI spec file should be generated in obj folder");
        var content = await File.ReadAllTextAsync(specFiles.First());
        content.Should().Contain("version:", because: "should contain version");
        content.Should().Contain("2.5.0", because: "should use custom version");
    }

    [Fact]
    public async Task ServerPackage_UsesCustomDocumentTitle_WhenSpecified()
    {
        var projectDir = await CreateTestProjectWithMessageTypesAsync("TestContracts.CustomTitle",
            eventTypes: ["TestContracts.CustomTitle.**"],
            documentTitle: "My Custom API Title",
            includeEventsAndCommands: true);

        // Act
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v minimal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        var specFiles = Directory.GetFiles(projectDir, "*.yaml", SearchOption.AllDirectories)
            .Where(f => f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .ToArray();
        specFiles.Should().NotBeEmpty(because: "AsyncAPI spec file should be generated in obj folder");
        var content = await File.ReadAllTextAsync(specFiles.First());
        content.Should().Contain("My Custom API Title", because: "should use custom title");
    }

    #endregion

        #region Helper Methods

        private async Task<string> CreateTestProjectWithMessageTypesAsync(
            string projectName,
            string[]? eventTypes = null,
            string[]? commandTypes = null,
            string? outputFormat = null,
            string? documentVersion = null,
            string? documentTitle = null,
            bool includeEventsAndCommands = false)
        {
            var projectDir = Path.Combine(_fixture.TestDir, projectName);
            Directory.CreateDirectory(projectDir);

            // Build property overrides - now includes event/command type patterns as properties
            var propertyOverrides = new List<string>();

            // Add event types as semicolon-separated property (not ItemGroup - MSBuild globs would consume wildcards)
            if (eventTypes != null && eventTypes.Length > 0)
                propertyOverrides.Add($"<ConcordIOEventTypes>{string.Join(";", eventTypes)}</ConcordIOEventTypes>");

            // Add command types as semicolon-separated property
            if (commandTypes != null && commandTypes.Length > 0)
                propertyOverrides.Add($"<ConcordIOCommandTypes>{string.Join(";", commandTypes)}</ConcordIOCommandTypes>");

            if (outputFormat != null)
                propertyOverrides.Add($"<ConcordIOAsyncApiOutputFormat>{outputFormat}</ConcordIOAsyncApiOutputFormat>");
            if (documentVersion != null)
                propertyOverrides.Add($"<ConcordIOAsyncApiDocumentVersion>{documentVersion}</ConcordIOAsyncApiDocumentVersion>");
            if (documentTitle != null)
                propertyOverrides.Add($"<ConcordIOAsyncApiDocumentTitle>{documentTitle}</ConcordIOAsyncApiDocumentTitle>");

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
                    <PackageReference Include="ConcordIO.AsyncApi.Server" Version="*" />
                  </ItemGroup>
                </Project>
                """;
            await File.WriteAllTextAsync(Path.Combine(projectDir, $"{projectName}.csproj"), csproj);

            // Create nuget.config
            await CreateNuGetConfigAsync(projectDir);

            // Create sample message types
            await CreateSampleMessageTypesAsync(projectDir, projectName, includeEventsAndCommands);

            return projectDir;
        }

        private static async Task CreateSampleMessageTypesAsync(string projectDir, string rootNamespace, bool includeEventsAndCommands)
        {
            if (includeEventsAndCommands)
            {
                // Create events
                var eventsContent = $$"""
                    namespace {{rootNamespace}}.Events;

                    public class OrderCreatedEvent
                    {
                        public Guid OrderId { get; set; }
                        public DateTime CreatedAt { get; set; }
                    }

                    public class OrderCancelledEvent
                    {
                        public Guid OrderId { get; set; }
                        public string Reason { get; set; } = string.Empty;
                    }
                    """;
                await File.WriteAllTextAsync(Path.Combine(projectDir, "Events.cs"), eventsContent);

                // Create commands
                var commandsContent = $$"""
                    namespace {{rootNamespace}}.Commands;

                    public class CreateOrderCommand
                    {
                        public Guid CustomerId { get; set; }
                        public List<OrderItem> Items { get; set; } = [];
                    }

                    public class OrderItem
                    {
                        public string ProductId { get; set; } = string.Empty;
                        public int Quantity { get; set; }
                    }
                    """;
                await File.WriteAllTextAsync(Path.Combine(projectDir, "Commands.cs"), commandsContent);
            }
            else
            {
                // Create simple event types
                var content = $$"""
                    namespace {{rootNamespace}};

                    public class OrderCreatedEvent
                    {
                        public Guid OrderId { get; set; }
                        public DateTime CreatedAt { get; set; }
                        public Customer Customer { get; set; } = new();
                    }

                    public class OrderCancelledEvent
                    {
                        public Guid OrderId { get; set; }
                        public string Reason { get; set; } = string.Empty;
                    }

                    public class Customer
                    {
                        public Guid Id { get; set; }
                        public string Name { get; set; } = string.Empty;
                    }
                    """;
                await File.WriteAllTextAsync(Path.Combine(projectDir, "Messages.cs"), content);
            }
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
                Arguments = $"{command} {args}",
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Use test-local NuGet cache to avoid conflicts with global cache
            process.StartInfo.Environment["NUGET_PACKAGES"] = _fixture.NugetCacheDir;

            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            return (process.ExitCode, output + error);
        }

        #endregion
    }
