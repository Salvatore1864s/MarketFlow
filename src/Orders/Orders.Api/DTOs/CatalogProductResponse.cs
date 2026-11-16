namespace Orders.Api.DTOs;

public record CatalogProductResponse(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    string Category,
    string SellerId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsActive
);