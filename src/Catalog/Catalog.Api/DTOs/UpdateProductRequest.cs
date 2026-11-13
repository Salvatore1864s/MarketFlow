
using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.DTOs;

public record UpdateProductRequest(
    [Required]
    [StringLength(150, MinimumLength = 2)]
    string Name,

    [StringLength(2000)]
    string Description,

    [Range(
        typeof(decimal),
        "0.01",
        "999999999",
        ParseLimitsInInvariantCulture = true)]
    decimal Price,

    [Required]
    [RegularExpression("^[A-Z]{3}$")]
    string Currency,

    [Required]
    [StringLength(100, MinimumLength = 2)]
    string Category,

    bool IsActive
);
