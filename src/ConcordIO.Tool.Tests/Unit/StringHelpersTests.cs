using ConcordIO.Tool.Services;
using FluentAssertions;

namespace ConcordIO.Tool.Tests.Unit;

public class StringHelpersTests
{
    [Theory]
    [InlineData("MyPackage", "MyPackage")]
    [InlineData("My.Package", "MyPackage")]
    [InlineData("My.Package.Name", "MyPackageName")]
    [InlineData("company.product.api", "CompanyProductApi")]
    [InlineData("A.B.C.D", "ABCD")]
    public void SanitizeClassName_ConvertsPackageIdToValidClassName(string input, string expected)
    {
        // Act
        var result = StringHelpers.SanitizeClassName(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("NSwag", "JsonLibrary", "NSwagJsonLibrary")]
    [InlineData("NSwag", "NSwagJsonLibrary", "NSwagJsonLibrary")]
    [InlineData("NSwag", "nswagJsonLibrary", "nswagJsonLibrary")] // Already has prefix (case-insensitive)
    [InlineData("Test", "Value", "TestValue")]
    [InlineData("Test", "TestValue", "TestValue")]
    public void NormalizePrefix_AddsOrKeepsPrefix(string prefix, string value, string expected)
    {
        // Act
        var result = StringHelpers.NormalizePrefix(prefix, value);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ParseKeyValuePairs_ReturnsEmptyArray_WhenInputIsNull()
    {
        // Act
        var result = StringHelpers.ParseKeyValuePairs(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseKeyValuePairs_ReturnsEmptyArray_WhenInputIsEmpty()
    {
        // Act
        var result = StringHelpers.ParseKeyValuePairs([]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseKeyValuePairs_ParsesSinglePair()
    {
        // Arrange
        var input = new[] { "key=value" };

        // Act
        var result = StringHelpers.ParseKeyValuePairs(input);

        // Assert
        result.Should().HaveCount(1);
        result[0].Key.Should().Be("key");
        result[0].Value.Should().Be("value");
    }

    [Fact]
    public void ParseKeyValuePairs_ParsesMultiplePairs()
    {
        // Arrange
        var input = new[] { "key1=value1", "key2=value2", "key3=value3" };

        // Act
        var result = StringHelpers.ParseKeyValuePairs(input);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(kvp => kvp.Key == "key1" && kvp.Value == "value1");
        result.Should().Contain(kvp => kvp.Key == "key2" && kvp.Value == "value2");
        result.Should().Contain(kvp => kvp.Key == "key3" && kvp.Value == "value3");
    }

    [Fact]
    public void ParseKeyValuePairs_TrimsWhitespace()
    {
        // Arrange
        var input = new[] { " key = value " };

        // Act
        var result = StringHelpers.ParseKeyValuePairs(input);

        // Assert
        result.Should().HaveCount(1);
        result[0].Key.Should().Be("key");
        result[0].Value.Should().Be("value");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("no-equals-sign")]
    [InlineData("")]
    public void ParseKeyValuePairs_ThrowsArgumentException_WhenFormatIsInvalid(string invalidPair)
    {
        // Arrange
        var input = new[] { invalidPair };

        // Act
        var act = () => StringHelpers.ParseKeyValuePairs(input);

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithMessage($"Invalid key=value format: '{invalidPair}'");
    }

    [Fact]
    public void ParseKeyValuePairs_AllowsDuplicateKeys()
    {
        // Arrange - This is expected behavior per the comment in the original code
        var input = new[] { "key=value1", "key=value2" };

        // Act
        var result = StringHelpers.ParseKeyValuePairs(input);

        // Assert
        result.Should().HaveCount(2);
        result.Where(kvp => kvp.Key == "key").Should().HaveCount(2);
    }
}
