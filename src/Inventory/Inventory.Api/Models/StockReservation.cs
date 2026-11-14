using Inventory.Api.Enums;

namespace Inventory.Api.Models;

public class StockReservation
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public ReservationStatus Status { get; set; }
        = ReservationStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}