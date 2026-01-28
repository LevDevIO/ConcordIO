using ConcordIO.AsyncApi.Server;
using ConcordIO.AsyncApi.Tests.TestTypes.Commands;
using ConcordIO.AsyncApi.Tests.TestTypes.Events;
using ConcordIO.AsyncApi.Tests.TestTypes.Inheritance;
using ConcordIO.AsyncApi.Tests.TestTypes.Interfaces;
using ConcordIO.AsyncApi.Tests.TestTypes.Nested;
using ConcordIO.AsyncApi.Tests.TestTypes.Nested.Level1;
using ConcordIO.AsyncApi.Tests.TestTypes.Nested.Level1.Level2;
using System.Reflection;

namespace ConcordIO.AsyncApi.Tests.Server;

public class TypeDiscoveryServiceTests
{
    private readonly TypeDiscoveryService _sut = new();
    private readonly Assembly _testAssembly = typeof(TypeDiscoveryServiceTests).Assembly;

    #region Namespace Wildcard Tests (Namespace.*)

    [Fact]
    public void DiscoverTypes_WithNamespaceWildcard_ReturnsTypesInExactNamespace()
    {
        // Arrange
        var patterns = new[]
        {
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.Events.*", MessageKind.Event)
        };

        // Act
        var result = _sut.DiscoverTypes(_testAssembly, patterns).ToList();

        // Assert
        result.Should().HaveCount(3);
        result.Select(r => r.Type).Should().Contain(typeof(OrderCreatedEvent));
        result.Select(r => r.Type).Should().Contain(typeof(OrderCancelledEvent));
        result.Select(r => r.Type).Should().Contain(typeof(OrderShippedEvent));
        
        // Abstract class should NOT be included
        result.Select(r => r.Type).Should().NotContain(typeof(OrderEventBase));
    }

    [Fact]
    public void DiscoverTypes_WithNamespaceWildcard_DoesNotIncludeNestedNamespaces()
    {
        // Arrange
        var patterns = new[]
        {
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.Nested.*", MessageKind.Event)
        };

        // Act
        var result = _sut.DiscoverTypes(_testAssembly, patterns).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Select(r => r.Type).Should().Contain(typeof(RootLevelEvent));
        result.Select(r => r.Type).Should().NotContain(typeof(Level1Event));
        result.Select(r => r.Type).Should().NotContain(typeof(Level2Event));
    }

    [Fact]
    public void DiscoverTypes_WithNamespaceWildcard_AssignsCorrectMessageKind()
    {
        // Arrange
        var patterns = new[]
        {
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.Commands.*", MessageKind.Command)
        };

        // Act
        var result = _sut.DiscoverTypes(_testAssembly, patterns).ToList();

        // Assert
        result.Should().AllSatisfy(r => r.Kind.Should().Be(MessageKind.Command));
    }

    #endregion

    #region Recursive Wildcard Tests (Namespace.**)

    [Fact]
    public void DiscoverTypes_WithRecursiveWildcard_ReturnsTypesInNamespaceAndSubNamespaces()
    {
        // Arrange
        var patterns = new[]
        {
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.Nested.**", MessageKind.Event)
        };

        // Act
        var result = _sut.DiscoverTypes(_testAssembly, patterns).ToList();

        // Assert
        result.Should().HaveCount(3);
        result.Select(r => r.Type).Should().Contain(typeof(RootLevelEvent));
        result.Select(r => r.Type).Should().Contain(typeof(Level1Event));
        result.Select(r => r.Type).Should().Contain(typeof(Level2Event));
    }

    [Fact]
    public void DiscoverTypes_WithRecursiveWildcard_FromRootTestTypes_ReturnsAllPublicTypes()
    {
        // Arrange
        var patterns = new[]
        {
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.**", MessageKind.Event)
        };

        // Act
        var result = _sut.DiscoverTypes(_testAssembly, patterns).ToList();

        // Assert - should include types from Events, Commands, Interfaces, Inheritance, and Nested namespaces
        result.Should().Contain(r => r.Type == typeof(OrderCreatedEvent));
        result.Should().Contain(r => r.Type == typeof(CreateOrderCommand));
        result.Should().Contain(r => r.Type == typeof(CustomerCreatedEvent));
        result.Should().Contain(r => r.Type == typeof(InventoryAddedEvent));
        result.Should().Contain(r => r.Type == typeof(RootLevelEvent));
        
        // Should NOT contain abstract/interface types
        result.Select(r => r.Type).Should().NotContain(typeof(OrderEventBase));
        result.Select(r => r.Type).Should().NotContain(typeof(InventoryEventBase));
        result.Select(r => r.Type).Should().NotContain(typeof(ICustomerEvent));
    }

    #endregion

    #region Interface Discovery Tests

    [Fact]
    public void DiscoverTypes_WithInterfacePattern_ReturnsAllImplementations()
    {
        // Arrange
        var patterns = new[]
        {
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.Interfaces.ICustomerEvent", MessageKind.Event)
        };

        // Act
        var result = _sut.DiscoverTypes(_testAssembly, patterns).ToList();

        // Assert
        result.Should().HaveCount(3);
        result.Select(r => r.Type).Should().Contain(typeof(CustomerCreatedEvent));
        result.Select(r => r.Type).Should().Contain(typeof(CustomerUpdatedEvent));
        result.Select(r => r.Type).Should().Contain(typeof(CustomerDeletedEvent));
        
        // UnrelatedEvent should NOT be included
        result.Select(r => r.Type).Should().NotContain(typeof(UnrelatedEvent));
        
        // Interface itself should NOT be included
        result.Select(r => r.Type).Should().NotContain(typeof(ICustomerEvent));
    }

    #endregion

    #region Base Class Discovery Tests

    [Fact]
    public void DiscoverTypes_WithAbstractBaseClassPattern_ReturnsAllSubclasses()
    {
        // Arrange
        var patterns = new[]
        {
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.Inheritance.InventoryEventBase", MessageKind.Event)
        };

        // Act
        var result = _sut.DiscoverTypes(_testAssembly, patterns).ToList();

        // Assert
        result.Should().HaveCount(3);
        result.Select(r => r.Type).Should().Contain(typeof(InventoryAddedEvent));
        result.Select(r => r.Type).Should().Contain(typeof(InventoryRemovedEvent));
        result.Select(r => r.Type).Should().Contain(typeof(InventoryAdjustedEvent));
        
        // Base class itself should NOT be included
        result.Select(r => r.Type).Should().NotContain(typeof(InventoryEventBase));
    }

    #endregion

    #region Concrete Type Tests

    [Fact]
    public void DiscoverTypes_WithConcreteTypePattern_ReturnsSingleType()
    {
        // Arrange
        var patterns = new[]
        {
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.Events.OrderCreatedEvent", MessageKind.Event)
        };

        // Act
        var result = _sut.DiscoverTypes(_testAssembly, patterns).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Type.Should().Be(typeof(OrderCreatedEvent));
        result[0].Kind.Should().Be(MessageKind.Event);
    }

    [Fact]
    public void DiscoverTypes_WithMultipleConcreteTypes_ReturnsAllSpecifiedTypes()
    {
        // Arrange
        var patterns = new[]
        {
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.Events.OrderCreatedEvent", MessageKind.Event),
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.Commands.CreateOrderCommand", MessageKind.Command)
        };

        // Act
        var result = _sut.DiscoverTypes(_testAssembly, patterns).ToList();

        // Assert
        result.Should().HaveCount(2);
        
        var orderCreated = result.First(r => r.Type == typeof(OrderCreatedEvent));
        orderCreated.Kind.Should().Be(MessageKind.Event);
        
        var createOrder = result.First(r => r.Type == typeof(CreateOrderCommand));
        createOrder.Kind.Should().Be(MessageKind.Command);
    }

    #endregion

    #region Mixed Pattern Tests

    [Fact]
    public void DiscoverTypes_WithMixedPatterns_CombinesResults()
    {
        // Arrange
        var patterns = new[]
        {
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.Events.*", MessageKind.Event),
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.Commands.*", MessageKind.Command)
        };

        // Act
        var result = _sut.DiscoverTypes(_testAssembly, patterns).ToList();

        // Assert
        // Events: 3 (OrderCreated, OrderCancelled, OrderShipped)
        // Commands: 3 (CreateOrder, CancelOrder, OrderItem)
        result.Should().HaveCount(6);
        
        result.Where(r => r.Kind == MessageKind.Event).Should().HaveCount(3);
        result.Where(r => r.Kind == MessageKind.Command).Should().HaveCount(3);
    }

    [Fact]
    public void DiscoverTypes_WithDuplicateTypeInMultiplePatterns_ReturnsTypeOnce_WithFirstKind()
    {
        // Arrange - same namespace with different kinds
        var patterns = new[]
        {
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.Events.*", MessageKind.Event),
            new MessageTypePattern("ConcordIO.AsyncApi.Tests.TestTypes.Events.*", MessageKind.Command) // duplicate
        };

        // Act
        var result = _sut.DiscoverTypes(_testAssembly, patterns).ToList();

        // Assert - should not have duplicates, first kind wins
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(r => r.Kind.Should().Be(MessageKind.Event));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void DiscoverTypes_WithEmptyPatterns_ReturnsEmpty()
    {
        // Arrange
        var patterns = Array.Empty<MessageTypePattern>();

        // Act
        var result = _sut.DiscoverTypes(_testAssembly, patterns).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverTypes_WithNonExistentNamespace_ReturnsEmpty()
    {
        // Arrange
        var patterns = new[]
        {
            new MessageTypePattern("NonExistent.Namespace.*", MessageKind.Event)
        };

        // Act
        var result = _sut.DiscoverTypes(_testAssembly, patterns).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverTypes_WithNullAssembly_ThrowsArgumentNullException()
    {
        // Arrange
        var patterns = new[] { new MessageTypePattern("Test.*", MessageKind.Event) };

        // Act & Assert
        var act = () => _sut.DiscoverTypes(null!, patterns);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DiscoverTypes_WithNullPatterns_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _sut.DiscoverTypes(_testAssembly, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
