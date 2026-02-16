using System.IO.Compression;

using FluentAssertions;

namespace ConcordIO.Tool.Tests.E2E;

/// <summary>
/// End-to-end tests for the 'pack' command that generates and packs
/// contract NuGet packages (.nupkg) in a single operation.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PackCommandIntegrationTests
{
	private readonly IntegrationTestFixture _fixture;

	public PackCommandIntegrationTests(IntegrationTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Pack_CreatesBothContractAndClientNupkgFiles()
	{
		using var ctx = _fixture.CreateTestContext(nameof(Pack_CreatesBothContractAndClientNupkgFiles));

		// Arrange
		var packageId = "PackTest.Api.Contracts";
		var version = "1.0.0";
		var specFileName = "openapi.yaml";
		var outputDir = Path.Combine(ctx.TestDir, "output");
		Directory.CreateDirectory(outputDir);

		// Write the spec file
		var specPath = Path.Combine(ctx.TestDir, specFileName);
		await File.WriteAllTextAsync(specPath, GetSimpleOpenApiSpec());

		// Act - Run pack command
		var args = $"pack --spec \"{specPath}\" --package-id {packageId} --version {version} --output \"{outputDir}\"";
		var (exitCode, output) = await ctx.RunToolAsync(args);

		// Assert
		exitCode.Should().Be(0, because: $"pack command should succeed. Output:\n{output}");

		// Check that .nupkg files were created
		var contractNupkg = Path.Combine(outputDir, $"{packageId}.{version}.nupkg");
		var clientNupkg = Path.Combine(outputDir, $"{packageId}.Client.{version}.nupkg");

		File.Exists(contractNupkg).Should().BeTrue(because: "contract .nupkg should be created");
		File.Exists(clientNupkg).Should().BeTrue(because: "client .nupkg should be created");

		output.Should().Contain(contractNupkg, because: "output should show contract package path");
		output.Should().Contain(clientNupkg, because: "output should show client package path");
	}

	[Fact]
	public async Task Pack_ContractPackageContainsSpecFilesInCorrectStructure()
	{
		using var ctx = _fixture.CreateTestContext(nameof(Pack_ContractPackageContainsSpecFilesInCorrectStructure));

		// Arrange
		var packageId = "PackStructureTest.Contracts";
		var version = "2.0.0";
		var specFileName = "api.yaml";
		var outputDir = Path.Combine(ctx.TestDir, "output");

		var specPath = Path.Combine(ctx.TestDir, specFileName);
		await File.WriteAllTextAsync(specPath, GetSimpleOpenApiSpec());

		// Act
		var args = $"pack --spec \"{specPath}\" --package-id {packageId} --version {version} --output \"{outputDir}\" --client false";
		var (exitCode, output) = await ctx.RunToolAsync(args);

		exitCode.Should().Be(0, because: $"pack should succeed. Output:\n{output}");

		// Extract and verify structure
		var nupkgPath = Path.Combine(outputDir, $"{packageId}.{version}.nupkg");
		var extractDir = Path.Combine(ctx.TestDir, "extracted");
		ZipFile.ExtractToDirectory(nupkgPath, extractDir);

		// Assert - Check package structure
		File.Exists(Path.Combine(extractDir, "openapi", specFileName)).Should().BeTrue(
			because: "spec should be in openapi/ folder for MSBuild items");
		File.Exists(Path.Combine(extractDir, "contentFiles", "any", "any", specFileName)).Should().BeTrue(
			because: "spec should be in contentFiles for IDE support");
		File.Exists(Path.Combine(extractDir, "build", $"{packageId}.targets")).Should().BeTrue(
			because: ".targets file should be in build/ folder");
	}

	[Fact]
	public async Task Pack_WithNoClientOption_CreatesOnlyContractPackage()
	{
		using var ctx = _fixture.CreateTestContext(nameof(Pack_WithNoClientOption_CreatesOnlyContractPackage));

		// Arrange
		var packageId = "PackNoClient.Contracts";
		var version = "1.0.0";
		var specFileName = "spec.yaml";
		var outputDir = Path.Combine(ctx.TestDir, "output");

		var specPath = Path.Combine(ctx.TestDir, specFileName);
		await File.WriteAllTextAsync(specPath, GetSimpleOpenApiSpec());

		// Act
		var args = $"pack --spec \"{specPath}\" --package-id {packageId} --version {version} --output \"{outputDir}\" --client false";
		var (exitCode, output) = await ctx.RunToolAsync(args);

		// Assert
		exitCode.Should().Be(0, because: $"pack should succeed. Output:\n{output}");

		var contractNupkg = Path.Combine(outputDir, $"{packageId}.{version}.nupkg");
		var clientNupkg = Path.Combine(outputDir, $"{packageId}.Client.{version}.nupkg");

		File.Exists(contractNupkg).Should().BeTrue(because: "contract .nupkg should be created");
		File.Exists(clientNupkg).Should().BeFalse(because: "client .nupkg should NOT be created when --client false");
	}

	[Fact]
	public async Task Pack_WithMultipleSpecs_IncludesAllInPackage()
	{
		using var ctx = _fixture.CreateTestContext(nameof(Pack_WithMultipleSpecs_IncludesAllInPackage));

		// Arrange
		var packageId = "PackMultiSpec.Contracts";
		var version = "1.0.0";
		var outputDir = Path.Combine(ctx.TestDir, "output");

		var openApiSpec = Path.Combine(ctx.TestDir, "api.yaml");
		var asyncApiSpec = Path.Combine(ctx.TestDir, "events.yaml");

		await File.WriteAllTextAsync(openApiSpec, GetSimpleOpenApiSpec());
		await File.WriteAllTextAsync(asyncApiSpec, GetSimpleAsyncApiSpec());

		// Act
		var args = $"pack --spec \"{openApiSpec}:openapi\" --spec \"{asyncApiSpec}:asyncapi\" --package-id {packageId} --version {version} --output \"{outputDir}\" --client false";
		var (exitCode, output) = await ctx.RunToolAsync(args);

		exitCode.Should().Be(0, because: $"pack should succeed. Output:\n{output}");

		// Extract and verify
		var nupkgPath = Path.Combine(outputDir, $"{packageId}.{version}.nupkg");
		var extractDir = Path.Combine(ctx.TestDir, "extracted");
		ZipFile.ExtractToDirectory(nupkgPath, extractDir);

		// Assert - Both specs should be in their respective folders
		File.Exists(Path.Combine(extractDir, "openapi", "api.yaml")).Should().BeTrue(
			because: "OpenAPI spec should be in openapi/ folder");
		File.Exists(Path.Combine(extractDir, "asyncapi", "events.yaml")).Should().BeTrue(
			because: "AsyncAPI spec should be in asyncapi/ folder");
	}

	[Fact]
	public async Task Pack_FailsGracefully_WhenSpecFileNotFound()
	{
		using var ctx = _fixture.CreateTestContext(nameof(Pack_FailsGracefully_WhenSpecFileNotFound));

		// Arrange
		var packageId = "PackMissingSpec.Contracts";
		var version = "1.0.0";
		var outputDir = Path.Combine(ctx.TestDir, "output");
		var nonExistentSpec = Path.Combine(ctx.TestDir, "does-not-exist.yaml");

		// Act
		var args = $"pack --spec \"{nonExistentSpec}\" --package-id {packageId} --version {version} --output \"{outputDir}\"";
		var (exitCode, output) = await ctx.RunToolAsync(args);

		// Assert
		exitCode.Should().NotBe(0, because: "pack should fail when spec file doesn't exist");
		output.Should().Contain("not found", because: "error message should indicate file not found");
	}

	[Fact]
	public async Task Pack_WithCustomClientPackageId_UsesProvidedId()
	{
		using var ctx = _fixture.CreateTestContext(nameof(Pack_WithCustomClientPackageId_UsesProvidedId));

		// Arrange
		var packageId = "PackCustomClient.Contracts";
		var clientPackageId = "PackCustomClient.MyCustomClient";
		var version = "1.0.0";
		var outputDir = Path.Combine(ctx.TestDir, "output");

		var specPath = Path.Combine(ctx.TestDir, "api.yaml");
		await File.WriteAllTextAsync(specPath, GetSimpleOpenApiSpec());

		// Act
		var args = $"pack --spec \"{specPath}\" --package-id {packageId} --version {version} --output \"{outputDir}\" --client-package-id {clientPackageId}";
		var (exitCode, output) = await ctx.RunToolAsync(args);

		// Assert
		exitCode.Should().Be(0, because: $"pack should succeed. Output:\n{output}");

		var clientNupkg = Path.Combine(outputDir, $"{clientPackageId}.{version}.nupkg");
		File.Exists(clientNupkg).Should().BeTrue(because: "client package should use custom ID");
	}

	[Fact]
	public async Task Pack_GeneratedPackage_CanBeUsedByConsumerProject()
	{
		using var ctx = _fixture.CreateTestContext(nameof(Pack_GeneratedPackage_CanBeUsedByConsumerProject));

		// Arrange - Create packages using pack command
		var packageId = "PackConsumerTest.Contracts";
		var version = "1.0.0";
		var specFileName = "petstore.yaml";
		var clientClassName = "PetStoreApiClient";

		var specPath = Path.Combine(ctx.TestDir, specFileName);
		await File.WriteAllTextAsync(specPath, GetPetStoreOpenApiSpec());

		// Pack the packages directly to the PackagesDir that nuget.config will reference
		var args = $"pack --spec \"{specPath}\" --package-id {packageId} --version {version} --output \"{ctx.PackagesDir}\" --client-class-name {clientClassName}";
		var (exitCode, output) = await ctx.RunToolAsync(args);
		exitCode.Should().Be(0, because: $"pack should succeed. Output:\n{output}");

		// Create a consumer project
		var projectDir = Path.Combine(ctx.TestDir, "ConsumerProject");
		Directory.CreateDirectory(projectDir);

		var csproj = """
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<OutputType>Library</OutputType>
	</PropertyGroup>
</Project>
""";
		await File.WriteAllTextAsync(Path.Combine(projectDir, "ConsumerProject.csproj"), csproj);
		await ctx.CreateNuGetConfigAsync(projectDir);

		// Add the client package
		var (addExitCode, addOutput) = await ctx.RunDotNetAsync("add", projectDir, $"package {packageId}.Client --version {version}");
		addExitCode.Should().Be(0, because: $"adding package should succeed. Output:\n{addOutput}");

		var consumerCode = """
		namespace ConsumerProject;

		public class ApiConsumer
		{
		}
		""";
		await File.WriteAllTextAsync(Path.Combine(projectDir, "ApiConsumer.cs"), consumerCode);

		// Act - Build the consumer project
		var (buildExitCode, buildOutput) = await ctx.RunDotNetAsync("build", projectDir, "-v normal");

		// Assert
		buildExitCode.Should().Be(0, because: $"build with packed package should succeed. Output:\n{buildOutput}");
	}

	#region Helper Methods

	private static string GetSimpleOpenApiSpec() => """
        openapi: "3.0.3"
        info:
          title: Test API
          version: "1.0.0"
        paths: {}
        """;

	private static string GetSimpleAsyncApiSpec() => """
        asyncapi: "3.0.0"
        info:
          title: Test Events
          version: "1.0.0"
        channels: {}
        """;

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
        """;

	#endregion
}
