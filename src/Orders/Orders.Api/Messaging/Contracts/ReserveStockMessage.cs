
namespace Orders.Api.Messaging.Contracts;

public class ReserveStockMessage
{
    public Guid OrderId { get; set; }

    public List<ReserveStockItemMessage> Items { get; set; } = [];
}

public class ReserveStockItemMessage
{
    public Guid ProductId { get; set; }

    public int Quantity { get; set; }
}
