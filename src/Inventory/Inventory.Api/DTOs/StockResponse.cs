namespace Inventory.Api.DTOs;

public record StockResponse(
    Guid ProductId,
    string SellerId,
    int AvailableQuantity,
    int ReservedQuantity,
    int InStock
);