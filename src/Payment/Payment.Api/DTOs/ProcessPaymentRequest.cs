
using System.ComponentModel.DataAnnotations;

namespace Payment.Api.DTOs;

public class ProcessPaymentRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public string BuyerId { get; set; } = string.Empty;

    [Range(0.01, 100000000)]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "RUB";
}
