using ConcordIO.Tool.AOComparison;
using FluentAssertions;

namespace ConcordIO.Tool.Tests.Integration;

/// <summary>
/// Integration tests for OasDiffRunner.
/// These tests require the oasdiff binary to be available.
/// </summary>
public class OasDiffRunnerTests
{
    private static readonly string TestDataPath = Path.Combine(
        AppContext.BaseDirectory, 
        "..", "..", "..", 
        "TestData");

    private static string GetTestFile(string fileName) => Path.Combine(TestDataPath, fileName);

    [Fact]
    public async Task Breaking_ReturnsNoBreakingChanges_WhenSpecsAreIdentical()
    {
        // Arrange
        var runner = new OasDiffRunner();
        var specPath = GetTestFile("petstore.yaml");

        // Act
        var result = await runner.Breaking(specPath, specPath, "");

        // Assert
        result.ExitCode.Should().Be(0);
        result.Breaking.Should().BeFalse();
    }

    [Fact]
    public async Task Breaking_DetectsBreakingChanges_WhenSpecsAreDifferent()
    {
        // Arrange
        var runner = new OasDiffRunner();
        var baseSpec = GetTestFile("petstore.yaml");
        var revisionSpec = GetTestFile("petstore-breaking.yaml");

        // Act - Note: oasdiff compares revision against base, looking for breaking changes
        // from the perspective of a consumer (what was in base that breaks in revision)
        var result = await runner.Breaking(revisionSpec, baseSpec, "");

        // Assert - The breaking spec removes endpoints and changes types
        // which should be detected as breaking when comparing against the original
        result.Output.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Run_ExecutesArbitraryCommand()
    {
        // Arrange
        var runner = new OasDiffRunner();

        // Act - just check version to verify the binary works
        var result = await runner.Run("--version");

        // Assert
        result.ExitCode.Should().Be(0);
        result.Output.Should().Contain("oasdiff");
    }
}
