using ConcordIO.AsyncApi.Server;
using ConcordIO.AsyncApi.Tests.TestTypes.Commands;
using ConcordIO.AsyncApi.Tests.TestTypes.Events;
using Neuroglia.AsyncApi;
using Neuroglia.AsyncApi.v3;

namespace ConcordIO.AsyncApi.Tests.Server;

public class AsyncApiDocumentGeneratorTests
{
    private readonly AsyncApiDocumentGenerator _sut = new();

    #region Basic Document Generation Tests

    [Fact]
    public void Generate_WithValidInputs_ReturnsDocument()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        result.Should().NotBeNull();
        result.AsyncApi.Should().Be(AsyncApiSpecVersion.V3);
    }

    [Fact]
    public void Generate_SetsCorrectTitle()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("MyService.Contracts", "1.0.0", types);

        // Assert
        result.Info.Title.Should().Be("MyService.Contracts");
    }

    [Fact]
    public void Generate_SetsCorrectVersion()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "2.5.0", types);

        // Assert
        result.Info.Version.Should().Be("2.5.0");
    }

    [Fact]
    public void Generate_SetsGeneratorDescription()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        result.Info.Description.Should().Contain("ConcordIO.AsyncApi.Server");
    }

    #endregion

    #region Channel Generation Tests

    [Fact]
    public void Generate_CreatesChannelForEachMessageType()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event),
            new DiscoveredType(typeof(OrderCancelledEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        result.Channels.Should().HaveCount(2);
    }

    [Fact]
    public void Generate_ChannelKeyIsFullTypeName()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        result.Channels.Should().ContainKey(typeof(OrderCreatedEvent).FullName!);
    }

    [Fact]
    public void Generate_ChannelAddressIsMassTransitUrnFormat()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        var channel = result.Channels[typeof(OrderCreatedEvent).FullName!];
        channel.Address.Should().Be($"urn:message:{typeof(OrderCreatedEvent).Namespace}:{nameof(OrderCreatedEvent)}");
    }

    [Fact]
    public void Generate_ChannelHasMessageReference()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        var channel = result.Channels[typeof(OrderCreatedEvent).FullName!];
        channel.Messages.Should().ContainKey(nameof(OrderCreatedEvent));
    }

    #endregion

    #region Message Generation Tests

    [Fact]
    public void Generate_CreatesMessageDefinitionForEachType()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event),
            new DiscoveredType(typeof(CreateOrderCommand), MessageKind.Command)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        result.Components!.Messages.Should().ContainKey(nameof(OrderCreatedEvent));
        result.Components!.Messages.Should().ContainKey(nameof(CreateOrderCommand));
    }

    [Fact]
    public void Generate_MessageHasCorrectContentType()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        var message = result.Components!.Messages![nameof(OrderCreatedEvent)];
        message.ContentType.Should().Be("application/json");
    }

    [Fact]
    public void Generate_MessageHasPayloadReference()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        var message = result.Components!.Messages![nameof(OrderCreatedEvent)];
        message.Payload.Should().NotBeNull();
        message.Payload!.Reference.Should().Be($"#/components/schemas/{nameof(OrderCreatedEvent)}");
    }

    #endregion

    #region Schema Generation Tests

    [Fact]
    public void Generate_CreatesSchemaForMessageType()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        result.Components!.Schemas.Should().ContainKey(nameof(OrderCreatedEvent));
    }

    [Fact]
    public void Generate_SchemaIncludesReferencedTypes()
    {
        // Arrange - CreateOrderCommand references OrderItem
        var types = new[]
        {
            new DiscoveredType(typeof(CreateOrderCommand), MessageKind.Command)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        result.Components!.Schemas.Should().ContainKey(nameof(CreateOrderCommand));
        result.Components!.Schemas.Should().ContainKey(nameof(OrderItem));
    }

    [Fact]
    public void Generate_SchemaHasCorrectFormat()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        var schema = result.Components!.Schemas![nameof(OrderCreatedEvent)];
        schema.SchemaFormat.Should().Contain("json");
    }

    #endregion

    #region Operation Generation Tests

    [Fact]
    public void Generate_CreatesOperationForEachType()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event),
            new DiscoveredType(typeof(CreateOrderCommand), MessageKind.Command)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        result.Operations.Should().ContainKey($"{nameof(OrderCreatedEvent)}Operation");
        result.Operations.Should().ContainKey($"{nameof(CreateOrderCommand)}Operation");
    }

    [Fact]
    public void Generate_EventsHaveReceiveOperation()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        var operation = result.Operations[$"{nameof(OrderCreatedEvent)}Operation"];
        operation.Action.Should().Be(V3OperationAction.Receive);
    }

    [Fact]
    public void Generate_CommandsHaveSendOperation()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(CreateOrderCommand), MessageKind.Command)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        var operation = result.Operations[$"{nameof(CreateOrderCommand)}Operation"];
        operation.Action.Should().Be(V3OperationAction.Send);
    }

    [Fact]
    public void Generate_OperationHasChannelReference()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        var operation = result.Operations[$"{nameof(OrderCreatedEvent)}Operation"];
        operation.Channel.Should().NotBeNull();
        operation.Channel!.Reference.Should().Contain(Uri.EscapeDataString(typeof(OrderCreatedEvent).FullName!));
    }

    #endregion

    #region Edge Cases and Validation Tests

    [Fact]
    public void Generate_WithEmptyTypes_ReturnsEmptyDocument()
    {
        // Arrange
        var types = Array.Empty<DiscoveredType>();

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        result.Should().NotBeNull();
        result.Channels.Should().BeEmpty();
        result.Components!.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Generate_WithNullTitle_ThrowsArgumentException()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var act = () => _sut.Generate(null!, "1.0.0", types);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_WithInvalidTitle_ThrowsArgumentException(string title)
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var act = () => _sut.Generate(title, "1.0.0", types);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Generate_WithNullVersion_ThrowsArgumentException()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var act = () => _sut.Generate("TestApi", null!, types);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_WithInvalidVersion_ThrowsArgumentException(string version)
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event)
        };

        // Act
        var act = () => _sut.Generate("TestApi", version, types);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Generate_WithNullTypes_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.Generate("TestApi", "1.0.0", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Multiple Types Tests

    [Fact]
    public void Generate_WithMultipleEventsAndCommands_GeneratesAllCorrectly()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event),
            new DiscoveredType(typeof(OrderCancelledEvent), MessageKind.Event),
            new DiscoveredType(typeof(OrderShippedEvent), MessageKind.Event),
            new DiscoveredType(typeof(CreateOrderCommand), MessageKind.Command),
            new DiscoveredType(typeof(CancelOrderCommand), MessageKind.Command)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        result.Channels.Should().HaveCount(5);
        result.Operations.Should().HaveCount(5);
        
        // Verify events have receive operations
        result.Operations[$"{nameof(OrderCreatedEvent)}Operation"].Action.Should().Be(V3OperationAction.Receive);
        result.Operations[$"{nameof(OrderCancelledEvent)}Operation"].Action.Should().Be(V3OperationAction.Receive);
        result.Operations[$"{nameof(OrderShippedEvent)}Operation"].Action.Should().Be(V3OperationAction.Receive);
        
        // Verify commands have send operations
        result.Operations[$"{nameof(CreateOrderCommand)}Operation"].Action.Should().Be(V3OperationAction.Send);
        result.Operations[$"{nameof(CancelOrderCommand)}Operation"].Action.Should().Be(V3OperationAction.Send);
    }

    [Fact]
    public void Generate_WithTypesFromDifferentNamespaces_CorrectlyHandlesAll()
    {
        // Arrange
        var types = new[]
        {
            new DiscoveredType(typeof(OrderCreatedEvent), MessageKind.Event),
            new DiscoveredType(typeof(CreateOrderCommand), MessageKind.Command)
        };

        // Act
        var result = _sut.Generate("TestApi", "1.0.0", types);

        // Assert
        // Verify channels have correct URNs based on namespaces
        var eventChannel = result.Channels[typeof(OrderCreatedEvent).FullName!];
        eventChannel.Address.Should().Contain("ConcordIO.AsyncApi.Tests.TestTypes.Events");

        var commandChannel = result.Channels[typeof(CreateOrderCommand).FullName!];
        commandChannel.Address.Should().Contain("ConcordIO.AsyncApi.Tests.TestTypes.Commands");
    }

    #endregion
}
