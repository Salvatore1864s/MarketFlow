using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Orders.Api.Models;

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Order? Order { get; set; }

    public Guid ProductId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [Precision(18, 2)]
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    [Precision(18, 2)]
    public decimal LineTotal { get; set; }
}