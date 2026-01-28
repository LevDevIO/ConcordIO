// Test types for base class inheritance discovery

namespace ConcordIO.AsyncApi.Tests.TestTypes.Inheritance;

/// <summary>
/// Abstract base class for inventory events
/// </summary>
public abstract class InventoryEventBase
{
    public string Sku { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class InventoryAddedEvent : InventoryEventBase
{
    public int QuantityAdded { get; set; }
}

public class InventoryRemovedEvent : InventoryEventBase
{
    public int QuantityRemoved { get; set; }
}

public class InventoryAdjustedEvent : InventoryEventBase
{
    public int NewQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}
