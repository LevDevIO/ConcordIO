using FluentAssertions;

namespace ConcordIO.Tool.Tests.E2E;

/// <summary>
/// Integration tests for the ConcordIO tool's AsyncAPI contract package generation.
/// Tests the full flow: generate contract package, generate client package, 
/// and verify the generated files are correct.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AsyncApiContractPackageIntegrationTests
{
	private readonly IntegrationTestFixture _fixture;

	public AsyncApiContractPackageIntegrationTests(IntegrationTestFixture fixture)
	{
		_fixture = fixture;
	}

	#region Contract Package Generation Tests

	[Fact]
	public async Task GenerateCommand_WithAsyncApiKind_GeneratesContractNuspec()
	{
		// Arrange
		using var ctx = _fixture.CreateTestContext(nameof(GenerateCommand_WithAsyncApiKind_GeneratesContractNuspec));
		var outputDir = Path.Combine(ctx.TestDir, "output");
		var specPath = await CreateSampleAsyncApiSpecAsync(outputDir, "asyncapi.yaml");

		// Act - use new syntax: --spec path:kind
		var (exitCode, output) = await ctx.RunToolAsync(
			$"generate --spec \"{specPath}:asyncapi\" --package-id MyService.Contracts --version 1.0.0 --output \"{outputDir}\" --client false");

		// Assert
		exitCode.Should().Be(0, because: $"generate should succeed. Output:\n{output}");

		var nuspecPath = Path.Combine(outputDir, "MyService.Contracts.nuspec");
		File.Exists(nuspecPath).Should().BeTrue(because: "nuspec should be generated");

		var nuspecContent = await File.ReadAllTextAsync(nuspecPath);
		nuspecContent.Should().Contain("<id>MyService.Contracts</id>");
		nuspecContent.Should().Contain("asyncapi/asyncapi.yaml", because: "asyncapi spec should be packaged in asyncapi/ folder");
	}

	[Fact]
	public async Task GenerateCommand_WithAsyncApiKind_GeneratesContractTargets()
	{
		// Arrange
		using var ctx = _fixture.CreateTestContext(nameof(GenerateCommand_WithAsyncApiKind_GeneratesContractTargets));
		var outputDir = Path.Combine(ctx.TestDir, "output");
		var specPath = await CreateSampleAsyncApiSpecAsync(outputDir, "asyncapi.yaml");

		// Act
		var (exitCode, output) = await ctx.RunToolAsync(
			$"generate --spec \"{specPath}:asyncapi\" --package-id MyService.Contracts --version 1.0.0 --output \"{outputDir}\" --client false");

		// Assert
		exitCode.Should().Be(0, because: $"generate should succeed. Output:\n{output}");

		var targetsPath = Path.Combine(outputDir, "build", "MyService.Contracts.targets");
		File.Exists(targetsPath).Should().BeTrue(because: "targets should be generated");

		var targetsContent = await File.ReadAllTextAsync(targetsPath);
		targetsContent.Should().Contain("ConcordIOAsyncApiContract", because: "asyncapi kind should expose ConcordIOAsyncApiContract items");
		targetsContent.Should().Contain(@"..\asyncapi\asyncapi.yaml", because: "should reference the asyncapi spec folder");
	}

	#endregion

	#region Client Package Generation Tests

	[Fact]
	public async Task GenerateCommand_WithAsyncApiKind_GeneratesClientNuspec()
	{
		// Arrange
		using var ctx = _fixture.CreateTestContext(nameof(GenerateCommand_WithAsyncApiKind_GeneratesClientNuspec));
		var outputDir = Path.Combine(ctx.TestDir, "output");
		var specPath = await CreateSampleAsyncApiSpecAsync(outputDir, "asyncapi.yaml");

		// Act
		var (exitCode, output) = await ctx.RunToolAsync(
			$"generate --spec \"{specPath}:asyncapi\" --package-id MyService.Contracts --version 1.0.0 --output \"{outputDir}\"");

		// Assert
		exitCode.Should().Be(0, because: $"generate should succeed. Output:\n{output}");

		var nuspecPath = Path.Combine(outputDir, "MyService.Contracts.Client.nuspec");
		File.Exists(nuspecPath).Should().BeTrue(because: "client nuspec should be generated");

		var nuspecContent = await File.ReadAllTextAsync(nuspecPath);
		nuspecContent.Should().Contain("<id>MyService.Contracts.Client</id>");
		nuspecContent.Should().Contain("ConcordIO.AsyncApi.Client", because: "asyncapi client should depend on ConcordIO.AsyncApi.Client");
		nuspecContent.Should().NotContain("NSwag", because: "asyncapi client should not depend on NSwag");
	}

	[Fact]
	public async Task GenerateCommand_WithAsyncApiKind_GeneratesClientTargets()
	{
		// Arrange
		using var ctx = _fixture.CreateTestContext(nameof(GenerateCommand_WithAsyncApiKind_GeneratesClientTargets));
		var outputDir = Path.Combine(ctx.TestDir, "output");
		var specPath = await CreateSampleAsyncApiSpecAsync(outputDir, "asyncapi.yaml");

		// Act
		var (exitCode, output) = await ctx.RunToolAsync(
			$"generate --spec \"{specPath}:asyncapi\" --package-id MyService.Contracts --version 1.0.0 --output \"{outputDir}\"");

		// Assert
		exitCode.Should().Be(0, because: $"generate should succeed. Output:\n{output}");

		var targetsPath = Path.Combine(outputDir, "build", "MyService.Contracts.Client.targets");
		File.Exists(targetsPath).Should().BeTrue(because: "client targets should be generated");

		var targetsContent = await File.ReadAllTextAsync(targetsPath);
		targetsContent.Should().Contain("ConcordIOAsyncApiContract", because: "asyncapi client should work with ConcordIOAsyncApiContract items");
		targetsContent.Should().NotContain("OpenApiReference", because: "asyncapi client should not use OpenApiReference");
	}

	#endregion

	#region End-to-End Integration Tests

	[Fact]
	public async Task AsyncApiContractPackage_GeneratesValidNuspecAndTargets()
	{
		// Arrange
		using var ctx = _fixture.CreateTestContext(nameof(AsyncApiContractPackage_GeneratesValidNuspecAndTargets));
		var outputDir = Path.Combine(ctx.TestDir, "output");
		var specPath = await CreateSampleAsyncApiSpecAsync(outputDir, "asyncapi.yaml");

		// Act
		var (exitCode, output) = await ctx.RunToolAsync(
			$"generate --spec \"{specPath}:asyncapi\" --package-id TestService.Contracts --version 1.0.0 --output \"{outputDir}\"");

		// Assert
		exitCode.Should().Be(0, because: $"generate should succeed. Output:\n{output}");

		// Verify contract package files
		var contractNuspec = await File.ReadAllTextAsync(Path.Combine(outputDir, "TestService.Contracts.nuspec"));
		contractNuspec.Should().Contain("<id>TestService.Contracts</id>");
		contractNuspec.Should().Contain("asyncapi/asyncapi.yaml");

		var contractTargets = await File.ReadAllTextAsync(Path.Combine(outputDir, "build", "TestService.Contracts.targets"));
		contractTargets.Should().Contain("ConcordIOAsyncApiContract");

		// Verify client package files
		var clientNuspec = await File.ReadAllTextAsync(Path.Combine(outputDir, "TestService.Contracts.Client.nuspec"));
		clientNuspec.Should().Contain("<id>TestService.Contracts.Client</id>");
		clientNuspec.Should().Contain("ConcordIO.AsyncApi.Client");
		clientNuspec.Should().NotContain("NSwag");

		var clientTargets = await File.ReadAllTextAsync(Path.Combine(outputDir, "build", "TestService.Contracts.Client.targets"));
		clientTargets.Should().Contain("ConcordIOAsyncApiContract");
		clientTargets.Should().NotContain("OpenApiReference");
	}

	[Fact]
	public async Task AsyncApiGeneration_ProducesCorrectDependencyChain()
	{
		// Arrange
		using var ctx = _fixture.CreateTestContext(nameof(AsyncApiGeneration_ProducesCorrectDependencyChain));
		var outputDir = Path.Combine(ctx.TestDir, "output");
		var specPath = await CreateSampleAsyncApiSpecAsync(outputDir, "asyncapi.yaml");

		// Act
		var (exitCode, output) = await ctx.RunToolAsync(
			$"generate --spec \"{specPath}:asyncapi\" --package-id TestService.Contracts --version 2.0.0 --output \"{outputDir}\"");

		// Assert
		exitCode.Should().Be(0);

		// Client should depend on contract with matching version
		var clientNuspec = await File.ReadAllTextAsync(Path.Combine(outputDir, "TestService.Contracts.Client.nuspec"));
		clientNuspec.Should().Contain("<dependency id=\"TestService.Contracts\" version=\"2.0.0\"");
		clientNuspec.Should().Contain("<dependency id=\"ConcordIO.AsyncApi.Client\"");
	}

	[Fact]
	public async Task MultipleSpecs_WithMixedKinds_GeneratesBothDependencies()
	{
		// Arrange
		using var ctx = _fixture.CreateTestContext(nameof(MultipleSpecs_WithMixedKinds_GeneratesBothDependencies));
		var outputDir = Path.Combine(ctx.TestDir, "output");
		var asyncApiSpec = await CreateSampleAsyncApiSpecAsync(outputDir, "events.yaml");
		var openApiSpec = await CreateSampleOpenApiSpecAsync(outputDir, "api.yaml");

		// Act - multiple specs with different kinds
		var (exitCode, output) = await ctx.RunToolAsync(
			$"generate --spec \"{openApiSpec}:openapi\" --spec \"{asyncApiSpec}:asyncapi\" --package-id MyService.Contracts --version 1.0.0 --output \"{outputDir}\"");

		// Assert
		exitCode.Should().Be(0, because: $"generate should succeed. Output:\n{output}");

		// Contract nuspec should include both specs
		var contractNuspec = await File.ReadAllTextAsync(Path.Combine(outputDir, "MyService.Contracts.nuspec"));
		contractNuspec.Should().Contain("openapi/api.yaml");
		contractNuspec.Should().Contain("asyncapi/events.yaml");

		// Contract targets should expose both item types
		var contractTargets = await File.ReadAllTextAsync(Path.Combine(outputDir, "build", "MyService.Contracts.targets"));
		contractTargets.Should().Contain("ConcordIOContract");
		contractTargets.Should().Contain("ConcordIOAsyncApiContract");

		// Client nuspec should depend on both NSwag and ConcordIO.AsyncApi.Client
		var clientNuspec = await File.ReadAllTextAsync(Path.Combine(outputDir, "MyService.Contracts.Client.nuspec"));
		clientNuspec.Should().Contain("NSwag.ApiDescription.Client");
		clientNuspec.Should().Contain("ConcordIO.AsyncApi.Client");

		// Client targets should have both targets
		var clientTargets = await File.ReadAllTextAsync(Path.Combine(outputDir, "build", "MyService.Contracts.Client.targets"));
		clientTargets.Should().Contain("OpenApiReference");
		clientTargets.Should().Contain("ConcordIOAsyncApiContract");
	}

	[Fact]
	public async Task MultipleSpecs_SameKind_GeneratesMultipleItems()
	{
		// Arrange
		using var ctx = _fixture.CreateTestContext(nameof(MultipleSpecs_SameKind_GeneratesMultipleItems));
		var outputDir = Path.Combine(ctx.TestDir, "output");
		var spec1 = await CreateSampleAsyncApiSpecAsync(outputDir, "events-v1.yaml");
		var spec2 = await CreateSampleAsyncApiSpecAsync(outputDir, "events-v2.yaml");

		// Act
		var (exitCode, output) = await ctx.RunToolAsync(
			$"generate --spec \"{spec1}:asyncapi\" --spec \"{spec2}:asyncapi\" --package-id MyService.Contracts --version 1.0.0 --output \"{outputDir}\"");

		// Assert
		exitCode.Should().Be(0, because: $"generate should succeed. Output:\n{output}");

		// Contract nuspec should include both specs
		var contractNuspec = await File.ReadAllTextAsync(Path.Combine(outputDir, "MyService.Contracts.nuspec"));
		contractNuspec.Should().Contain("asyncapi/events-v1.yaml");
		contractNuspec.Should().Contain("asyncapi/events-v2.yaml");

		// Contract targets should expose both as ConcordIOAsyncApiContract items
		var contractTargets = await File.ReadAllTextAsync(Path.Combine(outputDir, "build", "MyService.Contracts.targets"));
		contractTargets.Should().Contain("events-v1.yaml");
		contractTargets.Should().Contain("events-v2.yaml");
	}

	[Fact]
	public async Task DefaultKind_IsOpenApi_WhenNotSpecified()
	{
		// Arrange
		using var ctx = _fixture.CreateTestContext(nameof(DefaultKind_IsOpenApi_WhenNotSpecified));
		var outputDir = Path.Combine(ctx.TestDir, "output");
		var specPath = await CreateSampleOpenApiSpecAsync(outputDir, "api.yaml");

		// Act - no kind suffix, should default to openapi
		var (exitCode, output) = await ctx.RunToolAsync(
			$"generate --spec \"{specPath}\" --package-id MyService.Contracts --version 1.0.0 --output \"{outputDir}\"");

		// Assert
		exitCode.Should().Be(0, because: $"generate should succeed. Output:\n{output}");

		var contractNuspec = await File.ReadAllTextAsync(Path.Combine(outputDir, "MyService.Contracts.nuspec"));
		contractNuspec.Should().Contain("openapi/api.yaml", because: "default kind should be openapi");

		var clientNuspec = await File.ReadAllTextAsync(Path.Combine(outputDir, "MyService.Contracts.Client.nuspec"));
		clientNuspec.Should().Contain("NSwag.ApiDescription.Client", because: "openapi should use NSwag");
	}

	#endregion

	#region Helper Methods

	private static async Task<string> CreateSampleAsyncApiSpecAsync(string directory, string fileName)
	{
		Directory.CreateDirectory(directory);
		var specPath = Path.Combine(directory, fileName);

		var specContent = """
            asyncapi: '3.0.0'
            info:
              title: Test Service API
              version: 1.0.0
            channels:
              TestService.Contracts.Events.OrderCreatedEvent:
                address: 'urn:message:TestService.Contracts.Events:OrderCreatedEvent'
                messages:
                  OrderCreatedEvent:
                    $ref: '#/components/messages/OrderCreatedEvent'
            operations: {}
            components:
              messages:
                OrderCreatedEvent:
                  name: OrderCreatedEvent
                  contentType: application/json
                  payload:
                    $ref: '#/components/schemas/OrderCreatedEvent'
              schemas:
                OrderCreatedEvent:
                  schemaFormat: 'application/schema+json;version=draft-07'
                  schema:
                    type: object
                    x-dotnet-namespace: TestService.Contracts.Events
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

		await File.WriteAllTextAsync(specPath, specContent);
		return specPath;
	}

	private static async Task<string> CreateSampleOpenApiSpecAsync(string directory, string fileName)
	{
		Directory.CreateDirectory(directory);
		var specPath = Path.Combine(directory, fileName);

		var specContent = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: 1.0.0
            paths:
              /items:
                get:
                  operationId: getItems
                  responses:
                    '200':
                      description: Success
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Item'
            components:
              schemas:
                Item:
                  type: object
                  properties:
                    id:
                      type: string
                    name:
                      type: string
            """;

		await File.WriteAllTextAsync(specPath, specContent);
		return specPath;
	}

	#endregion
}
