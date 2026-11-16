using System.ComponentModel.DataAnnotations;

namespace Inventory.Api.DTOs;

public class UpdateStockRequest
{
    [Range(0, 1000000)]
    public int Quantity { get; set; }
}
