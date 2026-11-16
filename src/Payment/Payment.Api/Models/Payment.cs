
namespace Payment.Api.Models;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public string BuyerId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "RUB";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public string? FailureReason { get; set; }
}
