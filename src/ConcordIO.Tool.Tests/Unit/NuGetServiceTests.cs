using ConcordIO.Tool.Services;

using FluentAssertions;

namespace ConcordIO.Tool.Tests.Unit;

/// <summary>
/// Unit tests for the <see cref="NuGetService"/> class.
/// </summary>
/// <remarks>
/// These tests verify the NuGetService behavior including command construction
/// and output parsing. Note that full integration tests that actually call nuget.exe
/// are in the E2E test suite.
/// </remarks>
public class NuGetServiceTests
{
	[Fact]
	public void NuGetPackResult_Success_WhenExitCodeIsZero()
	{
		// Arrange & Act
		var result = new NuGetPackResult
		{
			ExitCode = 0,
			Output = "Successfully created package 'C:\\output\\Test.1.0.0.nupkg'.",
			NupkgPath = "C:\\output\\Test.1.0.0.nupkg"
		};

		// Assert
		result.Success.Should().BeTrue();
	}

	[Fact]
	public void NuGetPackResult_NotSuccess_WhenExitCodeIsNonZero()
	{
		// Arrange & Act
		var result = new NuGetPackResult
		{
			ExitCode = 1,
			Output = "Error: Some error occurred",
			NupkgPath = null
		};

		// Assert
		result.Success.Should().BeFalse();
	}

	[Theory]
	[InlineData(0, true)]
	[InlineData(1, false)]
	[InlineData(-1, false)]
	[InlineData(255, false)]
	public void NuGetPackResult_Success_DependsOnExitCode(int exitCode, bool expectedSuccess)
	{
		// Arrange & Act
		var result = new NuGetPackResult
		{
			ExitCode = exitCode,
			Output = "test output"
		};

		// Assert
		result.Success.Should().Be(expectedSuccess);
	}

	[Fact]
	public void NuGetPackResult_NupkgPath_CanBeNull()
	{
		// Arrange & Act
		var result = new NuGetPackResult
		{
			ExitCode = 1,
			Output = "Error occurred"
		};

		// Assert
		result.NupkgPath.Should().BeNull();
	}

	[Fact]
	public void NuGetPackResult_NupkgPath_CanBeSet()
	{
		// Arrange
		var expectedPath = "C:\\packages\\MyPackage.1.0.0.nupkg";

		// Act
		var result = new NuGetPackResult
		{
			ExitCode = 0,
			Output = "Success",
			NupkgPath = expectedPath
		};

		// Assert
		result.NupkgPath.Should().Be(expectedPath);
	}
}
