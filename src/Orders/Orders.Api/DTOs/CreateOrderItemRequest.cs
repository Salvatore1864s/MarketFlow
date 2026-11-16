using System.ComponentModel.DataAnnotations;

namespace Orders.Api.DTOs;

public class CreateOrderItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }
}
