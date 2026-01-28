// Test types for interface-based discovery

namespace ConcordIO.AsyncApi.Tests.TestTypes.Interfaces;

/// <summary>
/// Marker interface for customer events
/// </summary>
public interface ICustomerEvent
{
    Guid CustomerId { get; }
}

public class CustomerCreatedEvent : ICustomerEvent
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class CustomerUpdatedEvent : ICustomerEvent
{
    public Guid CustomerId { get; set; }
    public string NewName { get; set; } = string.Empty;
}

public class CustomerDeletedEvent : ICustomerEvent
{
    public Guid CustomerId { get; set; }
}

// This should NOT be discovered (doesn't implement ICustomerEvent)
public class UnrelatedEvent
{
    public string Data { get; set; } = string.Empty;
}
