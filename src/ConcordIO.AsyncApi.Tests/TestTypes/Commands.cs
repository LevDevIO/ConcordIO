// Test types for TypeDiscoveryService tests - Commands namespace

namespace ConcordIO.AsyncApi.Tests.TestTypes.Commands;

public class CreateOrderCommand
{
    public Guid CustomerId { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}

public class CancelOrderCommand
{
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
