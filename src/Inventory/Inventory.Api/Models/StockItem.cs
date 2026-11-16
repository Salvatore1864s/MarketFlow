namespace Inventory.Api.Models;

public class StockItem
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string SellerId { get; set; } = string.Empty;

    public int AvailableQuantity { get; set; }

    public int ReservedQuantity { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}