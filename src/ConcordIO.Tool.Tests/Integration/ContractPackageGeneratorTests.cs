using ConcordIO.Tool.Services;
using FluentAssertions;
using NSubstitute;

namespace ConcordIO.Tool.Tests.Integration;

public class ContractPackageGeneratorTests
{
    private readonly ITemplateRenderer _templateRenderer;
    private readonly IFileSystem _fileSystem;
    private readonly ContractPackageGenerator _sut;

    public ContractPackageGeneratorTests()
    {
        // Use real template renderer for integration tests
        _templateRenderer = new TemplateRenderer();
        _fileSystem = Substitute.For<IFileSystem>();
        _sut = new ContractPackageGenerator(_templateRenderer, _fileSystem);
    }

    [Fact]
    public async Task GenerateContractPackageAsync_CreatesNuspecAndTargetsFiles()
    {
        // Arrange
        var options = new ContractPackageOptions
        {
            PackageId = "MyCompany.Api.Contracts",
            Version = "1.2.3",
            Authors = "My Company",
            Description = "OpenAPI contracts for My Company API",
            SpecFileName = "api.yaml",
            Kind = "openapi",
            OutputDirectory = "/output"
        };

        string? capturedNuspecContent = null;
        string? capturedTargetsContent = null;

        _fileSystem.WriteAllTextAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci =>
            {
                var path = ci.ArgAt<string>(0);
                var content = ci.ArgAt<string>(1);
                if (path.EndsWith(".nuspec"))
                    capturedNuspecContent = content;
                else if (path.EndsWith(".targets"))
                    capturedTargetsContent = content;
            });

        // Act
        var result = await _sut.GenerateContractPackageAsync(options);

        // Assert
        _fileSystem.Received(1).CreateDirectory("/output");
        _fileSystem.Received(1).CreateDirectory(Path.Combine("/output", "build"));

        result.NuspecPath.Should().Be(Path.Combine("/output", "MyCompany.Api.Contracts.nuspec"));
        result.TargetsPath.Should().Be(Path.Combine("/output", "build", "MyCompany.Api.Contracts.targets"));

        result.NuspecContent.Should().Contain("<id>MyCompany.Api.Contracts</id>");
        result.NuspecContent.Should().Contain("<version>1.2.3</version>");
        result.NuspecContent.Should().Contain("<authors>My Company</authors>");
        result.NuspecContent.Should().Contain("api.yaml");

        result.TargetsContent.Should().Contain("<PackageId>MyCompany.Api.Contracts</PackageId>");
        result.TargetsContent.Should().Contain("<Kind>openapi</Kind>");
    }

    [Fact]
    public async Task GenerateClientPackageAsync_CreatesNuspecAndTargetsFiles()
    {
        // Arrange
        var options = new ClientPackageOptions
        {
            ClientPackageId = "MyCompany.Api.Client",
            ContractPackageId = "MyCompany.Api.Contracts",
            ContractVersion = "1.2.3",
            Version = "1.2.3",
            Authors = "My Company",
            Description = "Client for MyCompany API",
            Kind = "openapi",
            OutputDirectory = "/output",
            NSwagClientClassName = "MyCompanyApiClient",
            NSwagOutputPath = "MyCompanyApiClient",
            NSwagOptions = new List<KeyValuePair<string, string>>
            {
                new("NSwagJsonLibrary", "SystemTextJson"),
                new("NSwagGenerateExceptionClasses", "true")
            }
        };

        // Act
        var result = await _sut.GenerateClientPackageAsync(options);

        // Assert
        _fileSystem.Received(1).CreateDirectory("/output");
        _fileSystem.Received(1).CreateDirectory(Path.Combine("/output", "build"));

        result.NuspecPath.Should().Be(Path.Combine("/output", "MyCompany.Api.Client.nuspec"));
        result.TargetsPath.Should().Be(Path.Combine("/output", "build", "MyCompany.Api.Client.targets"));

        result.NuspecContent.Should().Contain("<id>MyCompany.Api.Client</id>");
        result.NuspecContent.Should().Contain("<dependency id=\"MyCompany.Api.Contracts\" version=\"1.2.3\"");

        result.TargetsContent.Should().Contain("<ClassName>MyCompanyApiClient</ClassName>");
        result.TargetsContent.Should().Contain("<OutputPath>MyCompanyApiClient.cs</OutputPath>");
        result.TargetsContent.Should().Contain("<NSwagJsonLibrary>SystemTextJson</NSwagJsonLibrary>");
    }

    [Fact]
    public async Task GenerateContractPackageAsync_IncludesPackageProperties()
    {
        // Arrange
        var options = new ContractPackageOptions
        {
            PackageId = "MyPackage",
            Version = "1.0.0",
            Authors = "Author",
            Description = "Description",
            SpecFileName = "spec.yaml",
            Kind = "openapi",
            OutputDirectory = "/output",
            PackageProperties =
            [
                new("projectUrl", "https://github.com/example/repo"),
                new("license", "MIT")
            ]
        };

        // Act
        var result = await _sut.GenerateContractPackageAsync(options);

        // Assert
        result.NuspecContent.Should().Contain("<projectUrl>https://github.com/example/repo</projectUrl>");
        result.NuspecContent.Should().Contain("<license>MIT</license>");
    }
}
