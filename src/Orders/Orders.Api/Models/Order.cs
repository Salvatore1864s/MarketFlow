using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Enums;

namespace Orders.Api.Models;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string BuyerId { get; set; } = string.Empty;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [Precision(18, 2)]
    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}