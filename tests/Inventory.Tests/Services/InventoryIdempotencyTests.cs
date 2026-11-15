
using Inventory.Api.Data;
using Inventory.Api.DTOs;
using Inventory.Api.Enums;
using Inventory.Api.Models;
using Inventory.Api.Services;
using Inventory.Api.Clients;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Inventory.Tests.Services;

public class InventoryIdempotencyTests
{
    private static InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics
                        .InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new InventoryDbContext(options);
    }

    private static InventoryService CreateService(
        InventoryDbContext context)
    {
        var handler = new TestHttpMessageHandler();

        var client = new CatalogClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://catalog.test/")
            });

        return new InventoryService(context, client);
    }

    [Fact]
    public async Task ReserveStockIdempotently_ReturnsTrue_WhenSameMessageIsRepeated()
    {
        using var context = CreateDbContext();

        var stock = new StockItem
        {
            ProductId = Guid.NewGuid(),
            SellerId = "seller-1",
            AvailableQuantity = 20,
            ReservedQuantity = 0
        };

        context.StockItems.Add(stock);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var request = new ReserveStockRequest
        {
            OrderId = Guid.NewGuid(),
            Items =
            [
                new ReserveStockItemRequest
                {
                    ProductId = stock.ProductId,
                    Quantity = 3
                }
            ]
        };

        var firstResult =
            await service.ReserveStockIdempotentlyAsync(request);

        var secondResult =
            await service.ReserveStockIdempotentlyAsync(request);

        Assert.True(firstResult);
        Assert.True(secondResult);

        Assert.Equal(3, stock.ReservedQuantity);
        Assert.Single(context.StockReservations);
    }

    [Fact]
    public async Task ReserveStockIdempotently_ReturnsFalse_WhenQuantityDiffers()
    {
        using var context = CreateDbContext();

        var stock = new StockItem
        {
            ProductId = Guid.NewGuid(),
            SellerId = "seller-1",
            AvailableQuantity = 20,
            ReservedQuantity = 0
        };

        context.StockItems.Add(stock);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var orderId = Guid.NewGuid();

        var firstRequest = new ReserveStockRequest
        {
            OrderId = orderId,
            Items =
            [
                new ReserveStockItemRequest
                {
                    ProductId = stock.ProductId,
                    Quantity = 3
                }
            ]
        };

        var secondRequest = new ReserveStockRequest
        {
            OrderId = orderId,
            Items =
            [
                new ReserveStockItemRequest
                {
                    ProductId = stock.ProductId,
                    Quantity = 5
                }
            ]
        };

        Assert.True(
            await service.ReserveStockIdempotentlyAsync(firstRequest));

        Assert.False(
            await service.ReserveStockIdempotentlyAsync(secondRequest));

        Assert.Equal(3, stock.ReservedQuantity);
        Assert.Single(context.StockReservations);
    }

    [Fact]
    public async Task ReserveStockIdempotently_ReturnsFalse_WhenReservationIsReleased()
    {
        using var context = CreateDbContext();

        var stock = new StockItem
        {
            ProductId = Guid.NewGuid(),
            SellerId = "seller-1",
            AvailableQuantity = 20,
            ReservedQuantity = 0
        };

        context.StockItems.Add(stock);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var request = new ReserveStockRequest
        {
            OrderId = Guid.NewGuid(),
            Items =
            [
                new ReserveStockItemRequest
                {
                    ProductId = stock.ProductId,
                    Quantity = 2
                }
            ]
        };

        Assert.True(
            await service.ReserveStockIdempotentlyAsync(request));

        Assert.True(
            await service.ReleaseReservationAsync(request.OrderId));

        Assert.False(
            await service.ReserveStockIdempotentlyAsync(request));

        Assert.Equal(0, stock.ReservedQuantity);
        Assert.Single(context.StockReservations);

        Assert.Equal(
            ReservationStatus.Released,
            context.StockReservations.Single().Status);
    }

    [Fact]
public async Task ConfirmReservation_ReturnsTrue_WhenCommandIsRepeated()
{
    using var context = CreateDbContext();

    var stock = new StockItem
    {
        ProductId = Guid.NewGuid(),
        SellerId = "seller-1",
        AvailableQuantity = 20,
        ReservedQuantity = 3
    };

    var orderId = Guid.NewGuid();

    context.StockItems.Add(stock);
    context.StockReservations.Add(new StockReservation
    {
        OrderId = orderId,
        ProductId = stock.ProductId,
        Quantity = 3,
        Status = ReservationStatus.Pending
    });

    await context.SaveChangesAsync();

    var service = CreateService(context);

    var firstResult =
        await service.ConfirmReservationAsync(orderId);

    var secondResult =
        await service.ConfirmReservationAsync(orderId);

    Assert.True(firstResult);
    Assert.True(secondResult);

    Assert.Equal(17, stock.AvailableQuantity);
    Assert.Equal(0, stock.ReservedQuantity);

    Assert.Equal(
        ReservationStatus.Confirmed,
        context.StockReservations.Single().Status);
}

[Fact]
public async Task ReleaseReservation_ReturnsTrue_WhenCommandIsRepeated()
{
    using var context = CreateDbContext();

    var stock = new StockItem
    {
        ProductId = Guid.NewGuid(),
        SellerId = "seller-1",
        AvailableQuantity = 20,
        ReservedQuantity = 3
    };

    var orderId = Guid.NewGuid();

    context.StockItems.Add(stock);
    context.StockReservations.Add(new StockReservation
    {
        OrderId = orderId,
        ProductId = stock.ProductId,
        Quantity = 3,
        Status = ReservationStatus.Pending
    });

    await context.SaveChangesAsync();

    var service = CreateService(context);

    var firstResult =
        await service.ReleaseReservationAsync(orderId);

    var secondResult =
        await service.ReleaseReservationAsync(orderId);

    Assert.True(firstResult);
    Assert.True(secondResult);

    Assert.Equal(20, stock.AvailableQuantity);
    Assert.Equal(0, stock.ReservedQuantity);

    Assert.Equal(
        ReservationStatus.Released,
        context.StockReservations.Single().Status);
}

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
