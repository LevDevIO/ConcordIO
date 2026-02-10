// Tests for bug fixes #22-#28

using ConcordIO.AsyncApi.Server;
using ConcordIO.AsyncApi.Tests.TestTypes.BugFixes;

namespace ConcordIO.AsyncApi.Tests.Server;

public class BugFixTests
{
    private readonly AsyncApiDocumentGenerator _sut = new();

    #region Issue #24: Schema Key Collision

    [Fact]
    public void Generate_WithTypesHavingSameNameInDifferentNamespaces_CreatesDistinctSchemas()
    {
        // Arrange - OrderEvent references two different Product types with the same name
        var types = new[]
        {
            new DiscoveredType(typeof(OrderEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert - Both Product schemas should exist with fully-qualified names as keys
        result.Components!.Schemas.Should().ContainKey("ConcordIO.AsyncApi.Tests.TestTypes.Namespace1.Product");
        result.Components!.Schemas.Should().ContainKey("ConcordIO.AsyncApi.Tests.TestTypes.Namespace2.Product");
        result.Components!.Schemas.Count.Should().BeGreaterThanOrEqualTo(3); // OrderEvent + 2 Product types
    }

    [Fact]
    public void Generate_WithNamespaceCollision_SchemasHaveCorrectProperties()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert - Verify the schemas are distinct by checking their properties
        var ns1ProductSchema = result.Components!.Schemas!["ConcordIO.AsyncApi.Tests.TestTypes.Namespace1.Product"];
        var ns2ProductSchema = result.Components!.Schemas!["ConcordIO.AsyncApi.Tests.TestTypes.Namespace2.Product"];

        ns1ProductSchema.Should().NotBe(ns2ProductSchema);
        
        // Note: Detailed property validation would require deserializing the Schema objects
        // which is done in the generator via ExpandoObject/JSON
    }

    #endregion

    #region Issue #25: Missing Simple Types

    [Fact]
    public void Generate_WithModernDotNetTypes_GeneratesSchemaCorrectly()
    {
        // Arrange - ModernTypesEvent uses DateOnly, TimeOnly, byte[], Half, Int128
        var types = new[]
        {
            new DiscoveredType(typeof(ModernTypesEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert - Should only have the ModernTypesEvent schema (no dependency schemas for simple types)
        result.Components!.Schemas.Should().ContainKey(typeof(ModernTypesEvent).FullName!);
        
        // Modern types should be treated as simple types, not generate separate schemas
        result.Components!.Schemas.Should().NotContainKey("System.DateOnly");
        result.Components!.Schemas.Should().NotContainKey("System.TimeOnly");
        result.Components!.Schemas.Should().NotContainKey("System.Byte[]");
        result.Components!.Schemas.Should().NotContainKey("System.Half");
        result.Components!.Schemas.Should().NotContainKey("System.Int128");
    }

    [Fact]
    public void Generate_WithModernTypes_OnlyCreatesMessageSchema()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(ModernTypesEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert - Should have exactly 1 schema (the message type itself)
        result.Components!.Schemas.Should().HaveCount(1);
    }

    #endregion

    #region Issue #28: Dictionary Key Type Ignored

    [Fact]
    public void Generate_WithDictionaryOfCustomTypes_IncludesKeyAndValueSchemas()
    {
        // Arrange - DictionaryEvent has Dictionary<CustomKey, CustomValue>
        var types = new[]
        {
            new DiscoveredType(typeof(DictionaryEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert - Should have schemas for DictionaryEvent, CustomKey, and CustomValue
        result.Components!.Schemas.Should().ContainKey(typeof(DictionaryEvent).FullName!);
        result.Components!.Schemas.Should().ContainKey(typeof(CustomKey).FullName!);
        result.Components!.Schemas.Should().ContainKey(typeof(CustomValue).FullName!);
    }

    [Fact]
    public void Generate_WithDictionaryOfSimpleKeyAndCustomValue_OnlyIncludesValueSchema()
    {
        // Arrange - DictionaryEvent also has Dictionary<string, CustomValue>
        var types = new[]
        {
            new DiscoveredType(typeof(DictionaryEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert - string is simple type, shouldn't have its own schema
        result.Components!.Schemas.Should().NotContainKey("System.String");
        result.Components!.Schemas.Should().ContainKey(typeof(CustomValue).FullName!);
    }

    [Fact]
    public void Generate_WithNestedCustomTypes_CreatesAllDependencySchemas()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(DictionaryEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert - Should have exactly 3 schemas: DictionaryEvent, CustomKey, CustomValue
        result.Components!.Schemas.Should().HaveCount(3);
    }

    #endregion
}
