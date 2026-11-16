using Orders.Api.Enums;

namespace Orders.Api.DTOs;

public class OrderResponse
{
    public Guid Id { get; set; }

    public string BuyerId { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<OrderItemResponse> Items { get; set; } = [];
}