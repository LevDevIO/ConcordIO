using System.Diagnostics;
using FluentAssertions;

namespace ConcordIO.Tool.Tests.E2E;

/// <summary>
/// End-to-end tests that verify the generated packages work correctly
/// when added to a real .NET project. These tests invoke the actual CLI tool
/// via dotnet run to ensure the full user experience is tested.
/// </summary>
public class PackageIntegrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _packagesDir;
    private readonly string _nugetCacheDir;
    private readonly string _toolProjectPath;

    public PackageIntegrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ConcordIO.Tests", Path.GetRandomFileName().Replace(".", ""));
        _packagesDir = Path.Combine(_testDir, "packages");
        _nugetCacheDir = Path.Combine(_testDir, "nuget-cache");
        Directory.CreateDirectory(_testDir);
        Directory.CreateDirectory(_packagesDir);
        Directory.CreateDirectory(_nugetCacheDir);

        // Find the tool project path relative to the test assembly
        var testAssemblyDir = Path.GetDirectoryName(typeof(PackageIntegrationTests).Assembly.Location)!;
        _toolProjectPath = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", "..", "ConcordIO.Tool", "ConcordIO.Tool.csproj"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_testDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    [Fact]
    public async Task ContractPackage_ExposesOpenApiSpecAsMSBuildItem()
    {
        // Arrange - Create the contract package
        var packageId = "TestCompany.Api.Contracts";
        var version = "1.0.0";
        var specFileName = "openapi.yaml";

        var packageDir = await CreateContractPackageAsync(packageId, version, specFileName);
        await CreateNuGetPackageAsync(packageDir, packageId);

        // Arrange - Create a test project that references the package
        var projectDir = Path.Combine(_testDir, "TestProject");
        await CreateTestProjectWithTargetPrintAsync(projectDir, packageId, version);

        // Act - Build the test project with our custom target that prints ConcordIOContract items
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-t:PrintConcordIOContracts -v minimal");

        // Assert - The build should succeed and show the contract item
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        output.Should().Contain(specFileName, because: "the ConcordIOContract item should contain the spec file name");
    }

    [Fact]
    public async Task ContractPackage_IncludesSpecFileInPackage()
    {
        // Arrange
        var packageId = "TestCompany.Api.Contracts";
        var version = "1.0.0";
        var specFileName = "openapi.yaml";

        var packageDir = await CreateContractPackageAsync(packageId, version, specFileName);

        // Create a sample spec file
        var specContent = """
            openapi: "3.0.3"
            info:
              title: Test API
              version: "1.0.0"
            paths: {}
            """;
        await File.WriteAllTextAsync(Path.Combine(packageDir, specFileName), specContent);

        await CreateNuGetPackageAsync(packageDir, packageId);

        // Act - Extract and verify the package contents
        var nupkgPath = Path.Combine(_packagesDir, $"{packageId}.{version}.nupkg");
        var extractDir = Path.Combine(_testDir, "extracted");
        System.IO.Compression.ZipFile.ExtractToDirectory(nupkgPath, extractDir);

        // Assert - Check the package structure
        File.Exists(Path.Combine(extractDir, "openapi", specFileName)).Should().BeTrue(
            because: "spec file should be in openapi/ folder");
        File.Exists(Path.Combine(extractDir, "contentFiles", "any", "any", specFileName)).Should().BeTrue(
            because: "spec file should be in contentFiles for IDE support");
        File.Exists(Path.Combine(extractDir, "build", $"{packageId}.targets")).Should().BeTrue(
            because: ".targets file should be in build/ folder");
    }

    [Fact]
    public async Task ClientPackage_DependsOnContractPackage()
    {
        // Arrange
        var contractPackageId = "TestCompany.Api.Contracts";
        var clientPackageId = "TestCompany.Api.Client";
        var version = "1.0.0";

        var clientPackageDir = await CreateClientPackageAsync(
            clientPackageId, contractPackageId, version, "TestApiClient");

        await CreateNuGetPackageAsync(clientPackageDir, clientPackageId);

        // Act - Extract and check the nuspec
        var nupkgPath = Path.Combine(_packagesDir, $"{clientPackageId}.{version}.nupkg");
        var extractDir = Path.Combine(_testDir, "extracted-client");
        System.IO.Compression.ZipFile.ExtractToDirectory(nupkgPath, extractDir);

        var nuspecPath = Path.Combine(extractDir, $"{clientPackageId}.nuspec");
        var nuspecContent = await File.ReadAllTextAsync(nuspecPath);

        // Assert
        nuspecContent.Should().Contain($"<dependency id=\"{contractPackageId}\"",
            because: "client package should depend on contract package");
        nuspecContent.Should().Contain("NSwag.ApiDescription.Client",
            because: "client package should depend on NSwag for code generation");
    }

    [Fact]
    public async Task ClientPackage_GeneratesClientCode_WhenProjectBuilds()
    {
        // Arrange - Create contract and client packages with a real OpenAPI spec using the CLI tool
        var contractPackageId = "TestCompany.PetStore.Contracts";
        var clientPackageId = "TestCompany.PetStore.Client";
        var version = "1.0.0";
        var specFileName = "petstore.yaml";
        var clientClassName = "PetStoreClient";

        // Use the CLI tool to generate both contract and client packages
        var packageDir = await GeneratePackagesWithToolAsync(
            contractPackageId, version, specFileName, GetPetStoreOpenApiSpec(), 
            clientClassName: clientClassName, 
            clientPackageId: clientPackageId);

        // Pack both packages
        await CreateNuGetPackageAsync(packageDir, contractPackageId);
        await CreateNuGetPackageAsync(packageDir, clientPackageId);

        // Create a test project that references the client (which transitively references contract)
        var projectDir = Path.Combine(_testDir, "ClientTestProject");
        await CreateTestProjectWithClientAsync(projectDir, clientPackageId, contractPackageId, version, clientClassName);

        // Act - Build the project (this should trigger NSwag code generation)
        var (exitCode, output) = await RunDotNetAsync("build", projectDir, "-v normal");

        // Assert
        exitCode.Should().Be(0, because: $"build should succeed with generated client. Output:\n{output}");
        
        // Verify the generated file exists
        var generatedFile = Path.Combine(projectDir, "obj", "Debug", "net8.0", $"{clientClassName}.cs");
        // NSwag might put it in different locations, so check the output for evidence of generation
        output.Should().Contain("NSwag", because: "NSwag should run during build");
    }

    [Fact]
    public async Task ClientPackage_IsMarkedAsDevelopmentDependency()
    {
        // Arrange
        var contractPackageId = "TestCompany.Api.Contracts";
        var clientPackageId = "TestCompany.Api.Client";
        var version = "1.0.0";

        var clientPackageDir = await CreateClientPackageAsync(
            clientPackageId, contractPackageId, version, "TestApiClient");

        await CreateNuGetPackageAsync(clientPackageDir, clientPackageId);

        // Act - Extract and check the nuspec
        var nupkgPath = Path.Combine(_packagesDir, $"{clientPackageId}.{version}.nupkg");
        var extractDir = Path.Combine(_testDir, "extracted-client-devdep");
        System.IO.Compression.ZipFile.ExtractToDirectory(nupkgPath, extractDir);

        var nuspecPath = Path.Combine(extractDir, $"{clientPackageId}.nuspec");
        var nuspecContent = await File.ReadAllTextAsync(nuspecPath);

        // Assert
        nuspecContent.Should().Contain("<developmentDependency>true</developmentDependency>",
            because: "client package should be marked as a development dependency");
    }

    [Fact]
    public async Task ClientPackage_DoesNotFlowTransitively_WhenAddedViaDotnetAdd()
    {
        // Arrange - Create contract and client packages
        var contractPackageId = "TestCompany.Transit.Contracts";
        var clientPackageId = "TestCompany.Transit.Client";
        var version = "1.0.0";
        var specFileName = "transit.yaml";
        var clientClassName = "TransitClient";

        var packageDir = await GeneratePackagesWithToolAsync(
            contractPackageId, version, specFileName, GetPetStoreOpenApiSpec(),
            clientClassName: clientClassName,
            clientPackageId: clientPackageId);

        await CreateNuGetPackageAsync(packageDir, contractPackageId);
        await CreateNuGetPackageAsync(packageDir, clientPackageId);

        // Create a library project that references the client package
        var libraryDir = Path.Combine(_testDir, "LibraryProject");
        Directory.CreateDirectory(libraryDir);
        
        var libraryCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Library</OutputType>
              </PropertyGroup>
            </Project>
            """;
        await File.WriteAllTextAsync(Path.Combine(libraryDir, "LibraryProject.csproj"), libraryCsproj);
        await CreateNuGetConfigAsync(libraryDir);
        await File.WriteAllTextAsync(Path.Combine(libraryDir, "Class1.cs"), "namespace LibraryProject { public class Class1 { } }");

        // Add client package to library
        var (addExitCode, addOutput) = await RunDotNetAsync("add", libraryDir, $"package {clientPackageId} --version {version}");
        addExitCode.Should().Be(0, because: $"adding package should succeed. Output:\n{addOutput}");

        // Build the library project first (this will run NSwag)
        var (libBuildExitCode, libBuildOutput) = await RunDotNetAsync("build", libraryDir, "-v minimal");
        libBuildExitCode.Should().Be(0, because: $"library build should succeed. Output:\n{libBuildOutput}");
        libBuildOutput.Should().Contain("NSwag", because: "NSwag should run for the library that directly references the client package");

        // Create a consumer project that references the library (not the client package)
        var consumerDir = Path.Combine(_testDir, "ConsumerProject");
        Directory.CreateDirectory(consumerDir);

        var consumerCsproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Library</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="{Path.Combine(libraryDir, "LibraryProject.csproj")}" />
              </ItemGroup>
            </Project>
            """;
        await File.WriteAllTextAsync(Path.Combine(consumerDir, "ConsumerProject.csproj"), consumerCsproj);
        await CreateNuGetConfigAsync(consumerDir);
        // add class with reference to the generated client
        await File.WriteAllTextAsync(Path.Combine(consumerDir, "ConsumerService.cs"), 
            "namespace ConsumerProject { public class ConsumerService { public LibraryProject.TransitClient Client; } }");

        // Act - Build only the consumer project (library is already built)
        // Use --no-dependencies to build only the consumer project itself
        var (exitCode, output) = await RunDotNetAsync("build", consumerDir, "-v minimal --no-dependencies");

        // Assert - Build should succeed and NSwag should NOT run for the consumer project
        exitCode.Should().Be(0, because: $"build should succeed. Output:\n{output}");
        output.Should().NotContain("NSwag", 
            because: "client package (development dependency) should not flow transitively to consumer project");
    }

    /// <summary>
    /// Runs the ConcordIO tool via dotnet run to generate contract and client packages.
    /// </summary>
    private async Task<string> GeneratePackagesWithToolAsync(
        string packageId, 
        string version, 
        string specFileName, 
        string specContent, 
        string? clientClassName = null,
        string? clientPackageId = null)
    {
        var outputDir = Path.Combine(_testDir, packageId);
        Directory.CreateDirectory(outputDir);

        // Write the spec file
        var specPath = Path.Combine(outputDir, specFileName);
        await File.WriteAllTextAsync(specPath, specContent);

        // Build CLI arguments
        var clientClassArg = clientClassName != null ? $"--client-class-name {clientClassName}" : "";
        var clientPackageArg = clientPackageId != null ? $"--client-package-id {clientPackageId}" : "";
        var args = $"generate --spec \"{specPath}\" --package-id {packageId} --version {version} --output \"{outputDir}\" {clientClassArg} {clientPackageArg}";

        // Run the tool
        var (exitCode, output) = await RunDotNetAsync("run", Path.GetDirectoryName(_toolProjectPath)!, $"--project \"{_toolProjectPath}\" -- {args}");

        if (exitCode != 0)
        {
            throw new Exception($"ConcordIO tool failed with exit code {exitCode}. Output:\n{output}");
        }

        return outputDir;
    }

    private async Task CreateTestProjectWithClientAsync(
        string projectDir, string clientPackageId, string contractPackageId, string version, string clientClassName)
    {
        Directory.CreateDirectory(projectDir);

        // Create a basic project first
        var csproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Library</OutputType>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """;
        await File.WriteAllTextAsync(Path.Combine(projectDir, "ClientTestProject.csproj"), csproj);

        // Create nuget.config before adding package so it can find our local packages
        await CreateNuGetConfigAsync(projectDir);

        // Add the client package using dotnet add package
        var (exitCode, output) = await RunDotNetAsync("add", projectDir, $"package {clientPackageId} --version {version}");
        if (exitCode != 0)
        {
            throw new Exception($"Failed to add client package: {output}");
        }

        // Create a class that uses the generated client to verify it compiles
        var usageClass = $$"""
            using System.Net.Http;

            namespace ClientTestProject;

            public class ApiService
            {
                private readonly HttpClient _httpClient;
                private readonly string _baseUrl;

                public ApiService(HttpClient httpClient, string baseUrl = "https://api.example.com")
                {
                    _httpClient = httpClient;
                    _baseUrl = baseUrl;
                }

                // This will fail to compile if the client wasn't generated
                public {{clientClassName}} CreateClient() => new {{clientClassName}}(_baseUrl, _httpClient);
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(projectDir, "ApiService.cs"), usageClass);
    }

    private static string GetPetStoreOpenApiSpec() => """
        openapi: "3.0.3"
        info:
          title: Pet Store API
          version: "1.0.0"
        paths:
          /pets:
            get:
              operationId: getPets
              summary: List all pets
              responses:
                '200':
                  description: A list of pets
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Pet'
            post:
              operationId: createPet
              summary: Create a pet
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Pet'
              responses:
                '201':
                  description: Pet created
          /pets/{petId}:
            get:
              operationId: getPetById
              summary: Get a pet by ID
              parameters:
                - name: petId
                  in: path
                  required: true
                  schema:
                    type: integer
                    format: int64
              responses:
                '200':
                  description: A pet
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Pet'
        components:
          schemas:
            Pet:
              type: object
              required:
                - id
                - name
              properties:
                id:
                  type: integer
                  format: int64
                name:
                  type: string
                tag:
                  type: string
        """;

    private async Task<string> CreateContractPackageAsync(string packageId, string version, string specFileName)
    {
        // Use the tool to generate contract package only (no client)
        var outputDir = Path.Combine(_testDir, packageId);
        Directory.CreateDirectory(outputDir);

        // Write a dummy spec file
        var specContent = """
            openapi: "3.0.3"
            info:
              title: Test API
              version: "1.0.0"
            paths: {}
            """;
        var specPath = Path.Combine(outputDir, specFileName);
        await File.WriteAllTextAsync(specPath, specContent);

        // Run the tool without client generation
        var args = $"generate --spec \"{specPath}\" --package-id {packageId} --version {version} --output \"{outputDir}\" --client false";
        var (exitCode, output) = await RunDotNetAsync("run", Path.GetDirectoryName(_toolProjectPath)!, $"--project \"{_toolProjectPath}\" -- {args}");

        if (exitCode != 0)
        {
            throw new Exception($"ConcordIO tool failed with exit code {exitCode}. Output:\n{output}");
        }

        return outputDir;
    }

    private async Task<string> CreateClientPackageAsync(
        string clientPackageId, string contractPackageId, string version, string clientClassName)
    {
        // Use the tool to generate client package
        // Note: The tool generates both contract and client, so we run it with client enabled
        var outputDir = Path.Combine(_testDir, clientPackageId);
        Directory.CreateDirectory(outputDir);

        // For client-only generation, we need a spec file. Create a dummy one.
        var specFileName = "openapi.yaml";
        var specContent = """
            openapi: "3.0.3"
            info:
              title: Test API
              version: "1.0.0"
            paths: {}
            """;
        var specPath = Path.Combine(outputDir, specFileName);
        await File.WriteAllTextAsync(specPath, specContent);

        // Run the tool - it will generate both contract and client packages in the output dir
        var args = $"generate --spec \"{specPath}\" --package-id {contractPackageId} --version {version} --output \"{outputDir}\" --client-package-id {clientPackageId} --client-class-name {clientClassName}";
        var (exitCode, output) = await RunDotNetAsync("run", Path.GetDirectoryName(_toolProjectPath)!, $"--project \"{_toolProjectPath}\" -- {args}");

        if (exitCode != 0)
        {
            throw new Exception($"ConcordIO tool failed with exit code {exitCode}. Output:\n{output}");
        }

        return outputDir;
    }

    private async Task CreateNuGetPackageAsync(string packageDir, string packageId)
    {
        var nuspecPath = Directory.GetFiles(packageDir, "*.nuspec")
            .First(f => Path.GetFileName(f).StartsWith(packageId, StringComparison.OrdinalIgnoreCase));
        var (exitCode, output) = await RunProcessAsync(
            "nuget",
            $"pack \"{nuspecPath}\" -OutputDirectory \"{_packagesDir}\"",
            packageDir);

        if (exitCode != 0)
        {
            throw new Exception($"Failed to create NuGet package: {output}");
        }
    }

    private async Task CreateTestProjectAsync(string projectDir, string packageId, string version)
    {
        Directory.CreateDirectory(projectDir);

        // Create a minimal test project
        var csproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Library</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="{packageId}" Version="{version}" />
              </ItemGroup>
            </Project>
            """;
        await File.WriteAllTextAsync(Path.Combine(projectDir, "TestProject.csproj"), csproj);

        await CreateNuGetConfigAsync(projectDir);
        await CreateDummyClassFileAsync(projectDir);
    }

    private async Task CreateTestProjectWithTargetPrintAsync(string projectDir, string packageId, string version)
    {
        Directory.CreateDirectory(projectDir);

        // Create a test project with a custom target to print ConcordIOContract items
        var csproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Library</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="{packageId}" Version="{version}" />
              </ItemGroup>
              <Target Name="PrintConcordIOContracts" DependsOnTargets="ResolvePackageAssets">
                <Message Importance="High" Text="ConcordIOContract items:" />
                <Message Importance="High" Text="  - %(ConcordIOContract.Identity)" Condition="'@(ConcordIOContract)' != ''" />
                <Message Importance="High" Text="  (none found)" Condition="'@(ConcordIOContract)' == ''" />
              </Target>
            </Project>
            """;
        await File.WriteAllTextAsync(Path.Combine(projectDir, "TestProject.csproj"), csproj);

        await CreateNuGetConfigAsync(projectDir);
        await CreateDummyClassFileAsync(projectDir);
    }

    private async Task CreateNuGetConfigAsync(string projectDir)
    {
        var nugetConfig = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="{_packagesDir}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """;
        await File.WriteAllTextAsync(Path.Combine(projectDir, "nuget.config"), nugetConfig);
    }

    private static async Task CreateDummyClassFileAsync(string projectDir)
    {
        await File.WriteAllTextAsync(
            Path.Combine(projectDir, "Class1.cs"),
            "namespace TestProject { public class Class1 { } }");
    }

    private async Task<(int ExitCode, string Output)> RunDotNetAsync(
        string command, string workingDir, string args = "")
    {
        return await RunProcessAsync("dotnet", $"{command} {args}", workingDir);
    }

    private async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName, string arguments, string workingDir)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Use test-local NuGet cache to avoid conflicts with global cache
        process.StartInfo.Environment["NUGET_PACKAGES"] = _nugetCacheDir;

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return (process.ExitCode, output + error);
    }
}
