using ConcordIO.Tool.Services;
using FluentAssertions;

namespace ConcordIO.Tool.Tests.Unit;

public class TemplateRendererTests
{
    private readonly TemplateRenderer _sut = new();

    [Fact]
    public async Task RenderAsync_ThrowsInvalidOperationException_WhenTemplateNotFound()
    {
        // Arrange
        var model = new Dictionary<string, object>();

        // Act
        var act = () => _sut.RenderAsync("NonExistent.Template", model);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Template not found: NonExistent.Template");
    }

    [Fact]
    public async Task RenderAsync_RendersContractNuspecTemplate()
    {
        // Arrange
        var model = new Dictionary<string, object>
        {
            ["package_id"] = "TestPackage",
            ["version"] = "1.0.0",
            ["authors"] = "Test Author",
            ["description"] = "Test Description",
            ["spec_file"] = "openapi.yaml",
            ["contract_kind"] = "openapi",
            ["package_properties"] = Array.Empty<KeyValuePair<string, string>>()
        };

        // Act
        var result = await _sut.RenderAsync("Contract.Contract.nuspec", model);

        // Assert
        result.Should().Contain("<id>TestPackage</id>");
        result.Should().Contain("<version>1.0.0</version>");
        result.Should().Contain("<authors>Test Author</authors>");
        result.Should().Contain("<description>Test Description</description>");
        result.Should().Contain("openapi.yaml");
    }

    [Fact]
    public async Task RenderAsync_RendersContractTargetsTemplate()
    {
        // Arrange
        var model = new Dictionary<string, object>
        {
            ["package_id"] = "TestPackage",
            ["version"] = "1.0.0",
            ["spec_file"] = "openapi.yaml",
            ["contract_kind"] = "openapi"
        };

        // Act
        var result = await _sut.RenderAsync("Contract.Contracts.targets", model);

        // Assert
        result.Should().Contain("<ConcordIOContract");
        result.Should().Contain("<PackageId>TestPackage</PackageId>");
        result.Should().Contain("<Kind>openapi</Kind>");
        result.Should().Contain("<Version>1.0.0</Version>");
    }

    [Fact]
    public async Task RenderAsync_RendersClientNuspecTemplate()
    {
        // Arrange
        var model = new Dictionary<string, object>
        {
            ["client_package_id"] = "TestPackage.Client",
            ["version"] = "1.0.0",
            ["authors"] = "Test Author",
            ["description"] = "Client for TestPackage",
            ["contract_package_id"] = "TestPackage",
            ["contract_version"] = "1.0.0",
            ["contract_kind"] = "openapi",
            ["package_properties"] = Array.Empty<KeyValuePair<string, string>>()
        };

        // Act
        var result = await _sut.RenderAsync("Contract.Client.Contract.Client.nuspec", model);

        // Assert
        result.Should().Contain("<id>TestPackage.Client</id>");
        result.Should().Contain("<dependency id=\"TestPackage\" version=\"1.0.0\"");
        result.Should().Contain("<dependency id=\"NSwag.ApiDescription.Client\"");
    }

    [Fact]
    public async Task RenderAsync_RendersClientTargetsTemplate()
    {
        // Arrange
        var nswagOptions = new List<KeyValuePair<string, string>>
        {
            new("NSwagJsonLibrary", "SystemTextJson"),
            new("NSwagGenerateExceptionClasses", "true")
        };

        var model = new Dictionary<string, object>
        {
            ["contract_package_id"] = "TestPackage",
            ["contract_version"] = "1.0.0",
            ["nswag_client_class_name"] = "TestClient",
            ["nswag_output_path"] = "TestClient",
            ["nswag_options"] = nswagOptions
        };

        // Act
        var result = await _sut.RenderAsync("Contract.Client.Contract.Client.targets", model);

        // Assert
        result.Should().Contain("<OpenApiReference");
        result.Should().Contain("<ClassName>TestClient</ClassName>");
        result.Should().Contain("<OutputPath>TestClient.cs</OutputPath>");
        result.Should().Contain("<NSwagJsonLibrary>SystemTextJson</NSwagJsonLibrary>");
        result.Should().Contain("<NSwagGenerateExceptionClasses>true</NSwagGenerateExceptionClasses>");
    }
}
