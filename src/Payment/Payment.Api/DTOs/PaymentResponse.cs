
using Payment.Api.Models;

namespace Payment.Api.DTOs;

public class PaymentResponse
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public string BuyerId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "RUB";

    public PaymentStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? FailureReason { get; set; }
}
