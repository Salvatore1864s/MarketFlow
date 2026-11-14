using System.ComponentModel.DataAnnotations;

namespace Inventory.Api.DTOs;

public class CreateStockRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 1000000)]
    public int Quantity { get; set; }
}