
using System.Security.Claims;
using Inventory.Api.DTOs;
using Inventory.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly InventoryService _inventoryService;

    public InventoryController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> GetStock(Guid productId)
    {
        var stock = await _inventoryService.GetStockAsync(productId);

        if (stock is null)
            return NotFound();

        return Ok(stock);
    }

    [HttpPost]
    [Authorize(Roles = "Seller,Admin")]
    public async Task<IActionResult> CreateStock(
        CreateStockRequest request)
    {
        var sellerId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(sellerId))
            return Unauthorized();

        var isAdmin = User.IsInRole("Admin");

        try
        {
            var stock = await _inventoryService.CreateStockAsync(
                request,
                sellerId,
                isAdmin);

            return CreatedAtAction(
                nameof(GetStock),
                new { productId = stock.ProductId },
                stock);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("reserve")]
    [Authorize(Roles = "Buyer")]
    public async Task<IActionResult> ReserveStock(
        ReserveStockRequest request)
    {
        var result = await _inventoryService.ReserveStockAsync(request);

        if (!result)
        {
            return Conflict(new
            {
                message = "Unable to reserve stock."
            });
        }

        return Ok(new
        {
            message = "Stock reserved successfully."
        });
    }

    [HttpPost("{orderId:guid}/confirm")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ConfirmReservation(Guid orderId)
    {
        var result =
            await _inventoryService.ConfirmReservationAsync(orderId);

        if (!result)
        {
            return Conflict(new
            {
                message = "Unable to confirm reservation."
            });
        }

        return Ok(new
        {
            message = "Reservation confirmed."
        });
    }

    [HttpPost("{orderId:guid}/release")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReleaseReservation(Guid orderId)
    {
        var result =
            await _inventoryService.ReleaseReservationAsync(orderId);

        if (!result)
        {
            return Conflict(new
            {
                message = "Unable to release reservation."
            });
        }

        return Ok(new
        {
            message = "Reservation released."
        });
    }
}
