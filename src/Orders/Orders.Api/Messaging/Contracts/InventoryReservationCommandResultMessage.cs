namespace Orders.Api.Messaging.Contracts;

public class InventoryReservationCommandResultMessage
{
    public Guid OrderId { get; set; }

    public string Command { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string? FailureReason { get; set; }
}