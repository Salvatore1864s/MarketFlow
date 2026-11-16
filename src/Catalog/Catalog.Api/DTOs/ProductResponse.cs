
namespace Catalog.Api.DTOs;

public record ProductResponse(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    string Category,
    string SellerId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsActive,
    string? ImageUrl
);
