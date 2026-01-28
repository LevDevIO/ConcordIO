// Test types for TypeDiscoveryService tests
// These types simulate message contracts in various namespaces

namespace ConcordIO.AsyncApi.Tests.TestTypes.Events;

public class OrderCreatedEvent
{
    public Guid OrderId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderCancelledEvent
{
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class OrderShippedEvent
{
    public Guid OrderId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
}

// Abstract class should not be discovered directly
public abstract class OrderEventBase
{
    public Guid OrderId { get; set; }
}
