
using System.Net;
using System.Text;
using System.Text.Json;
using Inventory.Api.Clients;
using Inventory.Api.Data;
using Inventory.Api.DTOs;
using Inventory.Api.Enums;
using Inventory.Api.Models;
using Inventory.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests.Services;

public class InventoryServiceTests
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

    private static CatalogProductResponse CreateProduct(
        Guid? id = null,
        string sellerId = "seller-1")
    {
        return new CatalogProductResponse(
            id ?? Guid.NewGuid(),
            "Samsung Galaxy S25 Ultra",
            "Смартфон, 512 ГБ",
            125000,
            "RUB",
            "Смартфоны",
            sellerId,
            DateTime.UtcNow,
            null,
            true);
    }

    private static CatalogClient CreateCatalogClient(
        params CatalogProductResponse[] products)
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            var productId = Guid.Parse(
                request.RequestUri!.Segments.Last());

            var product = products.FirstOrDefault(
                p => p.Id == productId);

            if (product is null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var json = JsonSerializer.Serialize(
                product,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://catalog.test/")
        };

        return new CatalogClient(httpClient);
    }

    private static InventoryService CreateService(
        InventoryDbContext context,
        params CatalogProductResponse[] products)
    {
        return new InventoryService(
            context,
            CreateCatalogClient(products));
    }

    private static async Task<StockItem> AddStockAsync(
        InventoryDbContext context,
        Guid? productId = null,
        int quantity = 20)
    {
        var stock = new StockItem
        {
            ProductId = productId ?? Guid.NewGuid(),
            SellerId = "seller-1",
            AvailableQuantity = quantity,
            ReservedQuantity = 0
        };

        context.StockItems.Add(stock);
        await context.SaveChangesAsync();

        return stock;
    }

    [Fact]
    public async Task GetStock_ReturnsStock_WhenProductExists()
    {
        using var context = CreateDbContext();

        var stock = await AddStockAsync(context);

        var service = CreateService(context);

        var result = await service.GetStockAsync(stock.ProductId);

        Assert.NotNull(result);
        Assert.Equal(stock.ProductId, result.ProductId);
        Assert.Equal(20, result.AvailableQuantity);
        Assert.Equal(0, result.ReservedQuantity);
        Assert.Equal(20, result.InStock);
    }

    [Fact]
    public async Task GetStock_ReturnsNull_WhenProductDoesNotExist()
    {
        using var context = CreateDbContext();

        var service = CreateService(context);

        var result = await service.GetStockAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateStock_CreatesStock_WhenProductExists()
    {
        using var context = CreateDbContext();

        var product = CreateProduct();

        var service = CreateService(context, product);

        var result = await service.CreateStockAsync(
            new CreateStockRequest
            {
                ProductId = product.Id,
                Quantity = 15
            },
            "seller-1",
            false);

        Assert.Equal(product.Id, result.ProductId);
        Assert.Equal("seller-1", result.SellerId);
        Assert.Equal(15, result.AvailableQuantity);
        Assert.Equal(0, result.ReservedQuantity);
        Assert.Single(context.StockItems);
    }

    [Fact]
    public async Task CreateStock_Throws_WhenProductDoesNotExist()
    {
        using var context = CreateDbContext();

        var service = CreateService(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateStockAsync(
                new CreateStockRequest
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 10
                },
                "seller-1",
                false));

        Assert.Empty(context.StockItems);
    }

    [Fact]
    public async Task CreateStock_Throws_WhenSellerDoesNotOwnProduct()
    {
        using var context = CreateDbContext();

        var product = CreateProduct(sellerId: "seller-2");

        var service = CreateService(context, product);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateStockAsync(
                new CreateStockRequest
                {
                    ProductId = product.Id,
                    Quantity = 10
                },
                "seller-1",
                false));

        Assert.Empty(context.StockItems);
    }

    [Fact]
    public async Task CreateStock_Throws_WhenStockAlreadyExists()
    {
        using var context = CreateDbContext();

        var product = CreateProduct();

        await AddStockAsync(context, product.Id);

        var service = CreateService(context, product);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateStockAsync(
                new CreateStockRequest
                {
                    ProductId = product.Id,
                    Quantity = 10
                },
                "seller-1",
                false));

        Assert.Single(context.StockItems);
    }

    [Fact]
    public async Task ReserveStock_ReservesQuantity_WhenStockIsAvailable()
    {
        using var context = CreateDbContext();

        var stock = await AddStockAsync(context);

        var service = CreateService(context);

        var orderId = Guid.NewGuid();

        var result = await service.ReserveStockAsync(
            new ReserveStockRequest
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
            });

        Assert.True(result);

        Assert.Equal(20, stock.AvailableQuantity);
        Assert.Equal(3, stock.ReservedQuantity);

        var reservation = Assert.Single(context.StockReservations);

        Assert.Equal(orderId, reservation.OrderId);
        Assert.Equal(3, reservation.Quantity);
        Assert.Equal(ReservationStatus.Pending, reservation.Status);
    }

    [Fact]
    public async Task ReserveStock_ReturnsFalse_WhenQuantityIsInsufficient()
    {
        using var context = CreateDbContext();

        var stock = await AddStockAsync(context, quantity: 5);

        var service = CreateService(context);

        var result = await service.ReserveStockAsync(
            new ReserveStockRequest
            {
                OrderId = Guid.NewGuid(),
                Items =
                [
                    new ReserveStockItemRequest
                    {
                        ProductId = stock.ProductId,
                        Quantity = 10
                    }
                ]
            });

        Assert.False(result);
        Assert.Equal(0, stock.ReservedQuantity);
        Assert.Empty(context.StockReservations);
    }

    [Fact]
    public async Task ReserveStock_ReturnsFalse_WhenOrderAlreadyReserved()
    {
        using var context = CreateDbContext();

        var stock = await AddStockAsync(context);
        var service = CreateService(context);
        var orderId = Guid.NewGuid();

        var request = new ReserveStockRequest
        {
            OrderId = orderId,
            Items =
            [
                new ReserveStockItemRequest
                {
                    ProductId = stock.ProductId,
                    Quantity = 2
                }
            ]
        };

        var firstResult = await service.ReserveStockAsync(request);
        var secondResult = await service.ReserveStockAsync(request);

        Assert.True(firstResult);
        Assert.False(secondResult);

        Assert.Equal(2, stock.ReservedQuantity);
        Assert.Single(context.StockReservations);
    }

    [Fact]
    public async Task ReserveStock_ReturnsFalse_WhenProductDoesNotExist()
    {
        using var context = CreateDbContext();

        var service = CreateService(context);

        var result = await service.ReserveStockAsync(
            new ReserveStockRequest
            {
                OrderId = Guid.NewGuid(),
                Items =
                [
                    new ReserveStockItemRequest
                    {
                        ProductId = Guid.NewGuid(),
                        Quantity = 1
                    }
                ]
            });

        Assert.False(result);
        Assert.Empty(context.StockReservations);
    }

    [Fact]
    public async Task ConfirmReservation_DecreasesAvailableQuantity()
    {
        using var context = CreateDbContext();

        var stock = await AddStockAsync(context);
        var service = CreateService(context);
        var orderId = Guid.NewGuid();

        await service.ReserveStockAsync(
            new ReserveStockRequest
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
            });

        var result = await service.ConfirmReservationAsync(orderId);

        Assert.True(result);
        Assert.Equal(17, stock.AvailableQuantity);
        Assert.Equal(0, stock.ReservedQuantity);

        var reservation = Assert.Single(context.StockReservations);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public async Task ConfirmReservation_ReturnsTrue_WhenAlreadyConfirmed()
    {
        using var context = CreateDbContext();

        var stock = await AddStockAsync(context);
        var service = CreateService(context);
        var orderId = Guid.NewGuid();

        await service.ReserveStockAsync(
            new ReserveStockRequest
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
            });

        Assert.True(await service.ConfirmReservationAsync(orderId));

        var secondResult =
            await service.ConfirmReservationAsync(orderId);

        Assert.True(secondResult);
        Assert.Equal(17, stock.AvailableQuantity);
        Assert.Equal(0, stock.ReservedQuantity);
    }

    [Fact]
    public async Task ReleaseReservation_RestoresAvailableStock()
    {
        using var context = CreateDbContext();

        var stock = await AddStockAsync(context);
        var service = CreateService(context);
        var orderId = Guid.NewGuid();

        await service.ReserveStockAsync(
            new ReserveStockRequest
            {
                OrderId = orderId,
                Items =
                [
                    new ReserveStockItemRequest
                    {
                        ProductId = stock.ProductId,
                        Quantity = 2
                    }
                ]
            });

        var result = await service.ReleaseReservationAsync(orderId);

        Assert.True(result);
        Assert.Equal(20, stock.AvailableQuantity);
        Assert.Equal(0, stock.ReservedQuantity);

        var reservation = Assert.Single(context.StockReservations);
        Assert.Equal(ReservationStatus.Released, reservation.Status);
    }

    [Fact]
    public async Task ReleaseReservation_ReturnsTrue_WhenAlreadyReleased()
    {
        using var context = CreateDbContext();

        var stock = await AddStockAsync(context);
        var service = CreateService(context);
        var orderId = Guid.NewGuid();

        await service.ReserveStockAsync(
            new ReserveStockRequest
            {
                OrderId = orderId,
                Items =
                [
                    new ReserveStockItemRequest
                    {
                        ProductId = stock.ProductId,
                        Quantity = 2
                    }
                ]
            });

        Assert.True(await service.ReleaseReservationAsync(orderId));

        var secondResult =
            await service.ReleaseReservationAsync(orderId);

        Assert.True(secondResult);
        Assert.Equal(0, stock.ReservedQuantity);
        Assert.Equal(1, context.StockReservations.Count());
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage>
            _responseFactory;

        public FakeHttpMessageHandler(
            Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}
