namespace Inventory.Api.Messaging.Contracts;

public class ReservationCommandMessage
{
    public Guid OrderId { get; set; }
}