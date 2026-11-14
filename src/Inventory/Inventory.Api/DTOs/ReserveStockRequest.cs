using System.ComponentModel.DataAnnotations;

namespace Inventory.Api.DTOs;

public class ReserveStockRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public List<ReserveStockItemRequest> Items { get; set; } = [];
}

public class ReserveStockItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }
}