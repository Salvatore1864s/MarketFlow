namespace Inventory.Api.Messaging.Contracts;

public class ReservationCommandResultMessage
{
    public Guid OrderId { get; set; }

    public string Command { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string? FailureReason { get; set; }
}