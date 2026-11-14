
using Payment.Api.Models;

namespace Payment.Api.Messaging.Contracts;

public class PaymentProcessedMessage
{
    public Guid PaymentId { get; set; }

    public Guid OrderId { get; set; }

    public string BuyerId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "RUB";

    public PaymentStatus Status { get; set; }

    public string? FailureReason { get; set; }

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
