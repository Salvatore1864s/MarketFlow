namespace Orders.Api.Messaging.Contracts;

public class ReservationResultMessage
{
    public Guid OrderId { get; set; }

    public bool Success { get; set; }

    public string? FailureReason { get; set; }
}