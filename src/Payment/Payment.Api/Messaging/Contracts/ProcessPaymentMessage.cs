namespace Payment.Api.Messaging.Contracts;

public class ProcessPaymentMessage
{
    public Guid OrderId { get; set; }

    public string BuyerId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "RUB";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool SimulateFailure { get; set; } = false;
}