using ConcordIO.Tool.Services;

using FluentAssertions;

using NSubstitute;

using VerifyXunit;

namespace ConcordIO.Tool.Tests.Integration;

public class ContractPackageGeneratorSnapshotTests
{
	private readonly ITemplateRenderer _templateRenderer = new TemplateRenderer();
	private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();

	[Fact]
	public async Task GenerateContractPackage_NuspecContent_MatchesSnapshot()
	{
		// Arrange
		var generator = new ContractPackageGenerator(_templateRenderer, _fileSystem);
		var options = new ContractPackageOptions
		{
			PackageId = "Acme.PetStore.Contracts",
			Version = "2.1.0",
			Authors = "Acme Corporation",
			Description = "OpenAPI specification for the Pet Store API",
			OutputDirectory = "/output",
			PackageProperties =
			[
				new("projectUrl", "https://github.com/acme/petstore"),
				new("repositoryUrl", "https://github.com/acme/petstore.git")
			],
			SpecsByKind = new Dictionary<string, List<string>>
			{
				["openapi"] = ["petstore.yaml"]
			}
		};

		// Act
		var result = await generator.GenerateContractPackageAsync(options);

		// Assert
		await Verify(result.NuspecContent);
	}

	[Fact]
	public async Task GenerateContractPackage_TargetsContent_MatchesSnapshot()
	{
		// Arrange
		var generator = new ContractPackageGenerator(_templateRenderer, _fileSystem);
		var options = new ContractPackageOptions
		{
			PackageId = "Acme.PetStore.Contracts",
			Version = "2.1.0",
			Authors = "Acme Corporation",
			Description = "OpenAPI specification for the Pet Store API",
			OutputDirectory = "/output",
			SpecsByKind = new Dictionary<string, List<string>>
			{
				["openapi"] = ["petstore.yaml"]
			}
		};

		// Act
		var result = await generator.GenerateContractPackageAsync(options);

		// Assert
		await Verify(result.TargetsContent);
	}

	[Fact]
	public async Task GenerateClientPackage_NuspecContent_MatchesSnapshot()
	{
		// Arrange
		var generator = new ContractPackageGenerator(_templateRenderer, _fileSystem);
		var options = new ClientPackageOptions
		{
			ClientPackageId = "Acme.PetStore.Client",
			ContractPackageId = "Acme.PetStore.Contracts",
			ContractVersion = "2.1.0",
			Version = "2.1.0",
			Authors = "Acme Corporation",
			Description = "Generated client for Pet Store API",
			OutputDirectory = "/output",
			NSwagClientClassName = "PetStoreClient",
			NSwagOutputPath = "PetStoreClient",
			PackageProperties =
			[
				new("projectUrl", "https://github.com/acme/petstore")
			],
			SpecsByKind = new Dictionary<string, List<string>>
			{
				["openapi"] = ["petstore.yaml"]
			}
		};

		// Act
		var result = await generator.GenerateClientPackageAsync(options);

		// Assert
		await Verify(result.NuspecContent);
	}

	[Fact]
	public async Task GenerateClientPackage_TargetsContent_MatchesSnapshot()
	{
		// Arrange
		var generator = new ContractPackageGenerator(_templateRenderer, _fileSystem);
		var options = new ClientPackageOptions
		{
			ClientPackageId = "Acme.PetStore.Client",
			ContractPackageId = "Acme.PetStore.Contracts",
			ContractVersion = "2.1.0",
			Version = "2.1.0",
			Authors = "Acme Corporation",
			Description = "Generated client for Pet Store API",
			OutputDirectory = "/output",
			NSwagClientClassName = "PetStoreClient",
			NSwagOutputPath = "PetStoreClient",
			NSwagOptions =
			[
				new("NSwagJsonLibrary", "SystemTextJson"),
				new("NSwagJsonPolymorphicSerializationStyle", "SystemTextJson"),
				new("NSwagGenerateExceptionClasses", "true"),
				new("NSwagInjectHttpClient", "true")
			],
			SpecsByKind = new Dictionary<string, List<string>>
			{
				["openapi"] = ["petstore.yaml"]
			}
		};

		// Act
		var result = await generator.GenerateClientPackageAsync(options);

		// Assert
		await Verify(result.TargetsContent);
	}
}
