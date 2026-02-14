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
		result.Success.Should().BeTrue();
	}

	[Fact]
	public async Task Breaking_DetectsBreakingChanges_WhenSpecsAreDifferent()
	{
		// Arrange
		var runner = new OasDiffRunner();
		var baseSpec = GetTestFile("petstore.yaml");
		var revisionSpec = GetTestFile("petstore-breaking.yaml");

		// Act - oasdiff compares base to revision, detecting breaking changes
		// Use -o WARN to treat warnings as errors for proper exit code behavior
		var result = await runner.Breaking(baseSpec, revisionSpec, "-o WARN");

		// Assert - The breaking spec removes endpoints and changes types
		// which should be detected as breaking when comparing against the original
		result.Output.Should().NotBeNullOrEmpty();
		result.ExitCode.Should().Be(1);
		result.Breaking.Should().BeTrue();
		result.Success.Should().BeTrue();
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
		result.Success.Should().BeTrue();
	}

	[Fact]
	public async Task Breaking_ReturnsToolError_WhenInvalidFileProvided()
	{
		// Arrange
		var runner = new OasDiffRunner();
		var nonExistentFile = "nonexistent-file.yaml";

		// Act
		var result = await runner.Breaking(nonExistentFile, nonExistentFile, "");

		// Assert - Tool error should result in exit code > 1
		result.ExitCode.Should().BeGreaterThan(1);
		result.Breaking.Should().BeFalse();
		result.Success.Should().BeFalse();
	}
}
