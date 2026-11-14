using System.ComponentModel.DataAnnotations;

namespace Orders.Api.DTOs;

public class CreateOrderRequest
{
    [Required]
    [MinLength(1)]
    public List<CreateOrderItemRequest> Items { get; set; } = [];
}