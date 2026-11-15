
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.DTOs;
using Orders.Api.Enums;
using Orders.Api.Messaging.Contracts;
using Orders.Api.Models;
using Orders.Api.Services;

namespace Orders.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Buyer")]
public class OrdersController : ControllerBase
{
    private readonly OrdersDbContext _dbContext;
    private readonly CatalogClient _catalogClient;

    public OrdersController(
        OrdersDbContext dbContext,
        CatalogClient catalogClient)
    {
        _dbContext = dbContext;
        _catalogClient = catalogClient;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var buyerId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(buyerId))
        {
            return Unauthorized();
        }

        var orderItems = new List<OrderItem>();

        foreach (var item in request.Items)
        {
            var product = await _catalogClient.GetProductByIdAsync(
                item.ProductId,
                cancellationToken);

            if (product == null || !product.IsActive)
            {
                return BadRequest(new
                {
                    message = $"Товар {item.ProductId} не найден или неактивен."
                });
            }

            var lineTotal = product.Price * item.Quantity;

            orderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = item.Quantity,
                LineTotal = lineTotal
            });
        }

        var order = new Order
        {
            BuyerId = buyerId,
            Status = OrderStatus.Pending,
            TotalAmount = orderItems.Sum(item => item.LineTotal),
            Items = orderItems
        };

        var message = new ReserveStockMessage
        {
            OrderId = order.Id,
            Items = order.Items.Select(item =>
                new ReserveStockItemMessage
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                }).ToList()
        };

        var outboxMessage = new OutboxMessage
        {
            EventType = "inventory.reserve",
            Payload = JsonSerializer.Serialize(message)
        };

        _dbContext.Orders.Add(order);
        _dbContext.OutboxMessages.Add(outboxMessage);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = order.Id },
            ToResponse(order));
    }

    [HttpGet]
    public async Task<IActionResult> GetMyOrders(
        CancellationToken cancellationToken)
    {
        var buyerId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(buyerId))
        {
            return Unauthorized();
        }

        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.BuyerId == buyerId)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(orders.Select(ToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var buyerId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(buyerId))
        {
            return Unauthorized();
        }

        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .FirstOrDefaultAsync(
                order => order.Id == id &&
                         order.BuyerId == buyerId,
                cancellationToken);

        if (order == null)
        {
            return NotFound();
        }

        return Ok(ToResponse(order));
    }

    private static OrderResponse ToResponse(Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            BuyerId = order.BuyerId,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,
            Items = order.Items.Select(item => new OrderItemResponse
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                LineTotal = item.LineTotal
            }).ToList()
        };
    }
}
