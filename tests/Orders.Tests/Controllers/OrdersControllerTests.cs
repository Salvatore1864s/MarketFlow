
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Controllers;
using Orders.Api.Data;
using Orders.Api.DTOs;
using Orders.Api.Enums;
using Orders.Api.Models;
using Orders.Api.Services;

namespace Orders.Tests.Controllers;

public class OrdersControllerTests
{
    private static OrdersDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new OrdersDbContext(options);
    }

    private static CatalogProductResponse CreateProduct(
        Guid? id = null,
        decimal price = 1000)
    {
        return new CatalogProductResponse(
            id ?? Guid.NewGuid(),
            "Samsung Galaxy S25 Ultra",
            "Смартфон, 512 ГБ",
            price,
            "RUB",
            "Смартфоны",
            "seller-1",
            DateTime.UtcNow,
            null,
            true
        );
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

            if (product == null)
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

    private static OrdersController CreateController(
        OrdersDbContext context,
        CatalogClient catalogClient,
        string userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, "Buyer")
        };

        var identity = new ClaimsIdentity(claims, "Test");

        var controller = new OrdersController(
            context,
            catalogClient)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };

        return controller;
    }

    [Fact]
    public async Task Create_SavesOrder_WithCorrectTotalAmount()
    {
        using var context = CreateDbContext();

        var product = CreateProduct(price: 125000);
        var catalogClient = CreateCatalogClient(product);

        var controller = CreateController(
            context,
            catalogClient,
            "buyer-1");

        var request = new CreateOrderRequest
        {
            Items =
            [
                new CreateOrderItemRequest
                {
                    ProductId = product.Id,
                    Quantity = 2
                }
            ]
        };

        var result = await controller.Create(
            request,
            CancellationToken.None);

        var createdResult =
            Assert.IsType<CreatedAtActionResult>(result);

        var response =
            Assert.IsType<OrderResponse>(createdResult.Value);

        Assert.Equal("buyer-1", response.BuyerId);
        Assert.Equal(250000, response.TotalAmount);
        Assert.Equal(OrderStatus.Pending, response.Status);
        Assert.Single(response.Items);
        Assert.Equal(125000, response.Items[0].UnitPrice);
        Assert.Equal(2, response.Items[0].Quantity);
        Assert.Single(context.Orders);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenProductDoesNotExist()
    {
        using var context = CreateDbContext();

        var catalogClient = CreateCatalogClient();

        var controller = CreateController(
            context,
            catalogClient,
            "buyer-1");

        var request = new CreateOrderRequest
        {
            Items =
            [
                new CreateOrderItemRequest
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 1
                }
            ]
        };

        var result = await controller.Create(
            request,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.Orders);
    }

    [Fact]
    public async Task Create_UsesPriceFromCatalog_NotFromClient()
    {
        using var context = CreateDbContext();

        var product = CreateProduct(price: 125000);
        var catalogClient = CreateCatalogClient(product);

        var controller = CreateController(
            context,
            catalogClient,
            "buyer-1");

        var request = new CreateOrderRequest
        {
            Items =
            [
                new CreateOrderItemRequest
                {
                    ProductId = product.Id,
                    Quantity = 1
                }
            ]
        };

        var result = await controller.Create(
            request,
            CancellationToken.None);

        var createdResult =
            Assert.IsType<CreatedAtActionResult>(result);

        var response =
            Assert.IsType<OrderResponse>(createdResult.Value);

        Assert.Equal(125000, response.TotalAmount);
    }

    [Fact]
    public async Task GetMyOrders_ReturnsOnlyCurrentBuyersOrders()
    {
        using var context = CreateDbContext();

        context.Orders.AddRange(
            new Order
            {
                BuyerId = "buyer-1",
                TotalAmount = 1000
            },
            new Order
            {
                BuyerId = "buyer-2",
                TotalAmount = 2000
            });

        await context.SaveChangesAsync();

        var controller = CreateController(
            context,
            CreateCatalogClient(),
            "buyer-1");

        var result = await controller.GetMyOrders(
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);

        var orders = Assert.IsAssignableFrom<
            IEnumerable<OrderResponse>>(okResult.Value);

        var order = Assert.Single(orders);

        Assert.Equal("buyer-1", order.BuyerId);
        Assert.Equal(1000, order.TotalAmount);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenOrderBelongsToAnotherBuyer()
    {
        using var context = CreateDbContext();

        var order = new Order
        {
            BuyerId = "buyer-2",
            TotalAmount = 5000
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var controller = CreateController(
            context,
            CreateCatalogClient(),
            "buyer-1");

        var result = await controller.GetById(
            order.Id,
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetById_ReturnsOrder_WhenCurrentBuyerIsOwner()
    {
        using var context = CreateDbContext();

        var order = new Order
        {
            BuyerId = "buyer-1",
            TotalAmount = 5000
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var controller = CreateController(
            context,
            CreateCatalogClient(),
            "buyer-1");

        var result = await controller.GetById(
            order.Id,
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);

        var response = Assert.IsType<OrderResponse>(
            okResult.Value);

        Assert.Equal(order.Id, response.Id);
        Assert.Equal("buyer-1", response.BuyerId);
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenUserIdIsMissing()
    {
        using var context = CreateDbContext();

        var controller = new OrdersController(
            context,
            CreateCatalogClient())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity())
                }
            }
        };

        var request = new CreateOrderRequest
        {
            Items =
            [
                new CreateOrderItemRequest
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 1
                }
            ]
        };

        var result = await controller.Create(
            request,
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(context.Orders);
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
