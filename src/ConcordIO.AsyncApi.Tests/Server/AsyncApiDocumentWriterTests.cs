using ConcordIO.AsyncApi.Server;
using Neuroglia.AsyncApi;
using Neuroglia.AsyncApi.v3;

namespace ConcordIO.AsyncApi.Tests.Server;

public class AsyncApiDocumentWriterTests : IDisposable
{
    private readonly AsyncApiDocumentWriter _sut = new();
    private readonly string _tempDirectory;

    public AsyncApiDocumentWriterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"AsyncApiTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static V3AsyncApiDocument CreateTestDocument()
    {
        return new V3AsyncApiDocument
        {
            AsyncApi = AsyncApiSpecVersion.V3,
            Info = new V3ApiInfo
            {
                Title = "Test API",
                Version = "1.0.0",
                Description = "Test description"
            },
            Channels = [],
            Components = new V3ComponentDefinitionCollection
            {
                Messages = [],
                Schemas = []
            }
        };
    }

    #region WriteYamlAsync Tests

    [Fact]
    public async Task WriteYamlAsync_WithValidDocument_CreatesYamlFile()
    {
        // Arrange
        var document = CreateTestDocument();
        var outputPath = Path.Combine(_tempDirectory, "test.yaml");

        // Act
        await _sut.WriteYamlAsync(document, outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task WriteYamlAsync_WithValidDocument_WritesValidYamlContent()
    {
        // Arrange
        var document = CreateTestDocument();
        var outputPath = Path.Combine(_tempDirectory, "test.yaml");

        // Act
        await _sut.WriteYamlAsync(document, outputPath);

        // Assert
        var content = await File.ReadAllTextAsync(outputPath);
        content.Should().Contain("asyncapi:");
        content.Should().Contain("info:");
        content.Should().Contain("title: Test API");
        content.Should().Contain("version: '1.0.0'");
    }

    [Fact]
    public async Task WriteYamlAsync_WithNestedDirectory_CreatesDirectoryAndFile()
    {
        // Arrange
        var document = CreateTestDocument();
        var outputPath = Path.Combine(_tempDirectory, "nested", "dir", "test.yaml");

        // Act
        await _sut.WriteYamlAsync(document, outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        Directory.Exists(Path.GetDirectoryName(outputPath)).Should().BeTrue();
    }

    [Fact]
    public async Task WriteYamlAsync_WithNullDocument_ThrowsArgumentNullException()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDirectory, "test.yaml");

        // Act
        var act = () => _sut.WriteYamlAsync(null!, outputPath);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WriteYamlAsync_WithInvalidOutputPath_ThrowsArgumentException(string? outputPath)
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        var act = () => _sut.WriteYamlAsync(document, outputPath!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task WriteYamlAsync_WithCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var document = CreateTestDocument();
        var outputPath = Path.Combine(_tempDirectory, "test.yaml");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _sut.WriteYamlAsync(document, outputPath, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region WriteJsonAsync Tests

    [Fact]
    public async Task WriteJsonAsync_WithValidDocument_CreatesJsonFile()
    {
        // Arrange
        var document = CreateTestDocument();
        var outputPath = Path.Combine(_tempDirectory, "test.json");

        // Act
        await _sut.WriteJsonAsync(document, outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task WriteJsonAsync_WithValidDocument_WritesValidJsonContent()
    {
        // Arrange
        var document = CreateTestDocument();
        var outputPath = Path.Combine(_tempDirectory, "test.json");

        // Act
        await _sut.WriteJsonAsync(document, outputPath);

        // Assert
        var content = await File.ReadAllTextAsync(outputPath);
        content.Should().Contain("\"asyncapi\"");
        content.Should().Contain("\"info\"");
        content.Should().Contain("\"Test API\"");
        content.Should().Contain("\"1.0.0\"");
    }

    [Fact]
    public async Task WriteJsonAsync_WithNestedDirectory_CreatesDirectoryAndFile()
    {
        // Arrange
        var document = CreateTestDocument();
        var outputPath = Path.Combine(_tempDirectory, "nested", "dir", "test.json");

        // Act
        await _sut.WriteJsonAsync(document, outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        Directory.Exists(Path.GetDirectoryName(outputPath)).Should().BeTrue();
    }

    [Fact]
    public async Task WriteJsonAsync_WithNullDocument_ThrowsArgumentNullException()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDirectory, "test.json");

        // Act
        var act = () => _sut.WriteJsonAsync(null!, outputPath);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WriteJsonAsync_WithInvalidOutputPath_ThrowsArgumentException(string? outputPath)
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        var act = () => _sut.WriteJsonAsync(document, outputPath!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task WriteJsonAsync_WithCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var document = CreateTestDocument();
        var outputPath = Path.Combine(_tempDirectory, "test.json");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _sut.WriteJsonAsync(document, outputPath, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WriteJsonAsync_OutputIsValidJson()
    {
        // Arrange
        var document = CreateTestDocument();
        var outputPath = Path.Combine(_tempDirectory, "test.json");

        // Act
        await _sut.WriteJsonAsync(document, outputPath);

        // Assert
        var content = await File.ReadAllTextAsync(outputPath);
        var parseAction = () => System.Text.Json.JsonDocument.Parse(content);
        parseAction.Should().NotThrow("output should be valid JSON");
    }

    #endregion
}
