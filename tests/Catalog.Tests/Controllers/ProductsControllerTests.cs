
using System.Security.Claims;
using Catalog.Api.Controllers;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Catalog.Tests.Controllers;

public class ProductsControllerTests
{
    private static CatalogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CatalogDbContext(options);
    }

    private static ProductsController CreateController(
        CatalogDbContext context,
        string userId,
        string role = "Seller")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var user = new ClaimsPrincipal(identity);

        var controller = new ProductsController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user
                }
            }
        };

        return controller;
    }

    private static Product CreateProduct(
        string sellerId = "seller-1")
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Name = "iPhone 16 Pro",
            Description = "256 GB",
            Price = 95000,
            Currency = "RUB",
            Category = "Electronics",
            SellerId = sellerId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyActiveProducts()
    {
        using var context = CreateDbContext();

        context.Products.Add(CreateProduct());

        var inactiveProduct = CreateProduct();
        inactiveProduct.IsActive = false;

        context.Products.Add(inactiveProduct);

        await context.SaveChangesAsync();

        var controller = CreateController(context, "buyer-1");

        var result = await controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var products = Assert.IsAssignableFrom<IEnumerable<ProductResponse>>(
            okResult.Value);

        Assert.Single(products);
    }

    [Fact]
    public async Task GetById_ReturnsProduct_WhenProductExists()
    {
        using var context = CreateDbContext();

        var product = CreateProduct();

        context.Products.Add(product);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "buyer-1");

        var result = await controller.GetById(product.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProductResponse>(okResult.Value);

        Assert.Equal(product.Id, response.Id);
        Assert.Equal(product.Name, response.Name);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenProductIsInactive()
    {
        using var context = CreateDbContext();

        var product = CreateProduct();
        product.IsActive = false;

        context.Products.Add(product);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "buyer-1");

        var result = await controller.GetById(product.Id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_SavesProduct_WithCurrentSellerId()
    {
        using var context = CreateDbContext();

        var controller = CreateController(context, "seller-1");

        var request = new CreateProductRequest(
            "MacBook Pro",
            "16 GB RAM",
            150000,
            "RUB",
            "Electronics"
        );

        var result = await controller.Create(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<ProductResponse>(createdResult.Value);

        Assert.Equal("seller-1", response.SellerId);
        Assert.Equal("MacBook Pro", response.Name);
        Assert.Equal(150000, response.Price);
        Assert.Single(context.Products);
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenUserIdIsMissing()
    {
        using var context = CreateDbContext();

        var controller = new ProductsController(context)
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

        var request = new CreateProductRequest(
            "MacBook Pro",
            "16 GB RAM",
            150000,
            "RUB",
            "Electronics"
        );

        var result = await controller.Create(request);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(context.Products);
    }

    [Fact]
    public async Task Update_ChangesProduct_WhenCurrentUserIsOwner()
    {
        using var context = CreateDbContext();

        var product = CreateProduct("seller-1");

        context.Products.Add(product);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "seller-1");

        var request = new UpdateProductRequest(
            "iPhone 16 Pro Max",
            "512 GB",
            110000,
            "RUB",
            "Electronics",
            true
        );

        var result = await controller.Update(product.Id, request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProductResponse>(okResult.Value);

        Assert.Equal("iPhone 16 Pro Max", response.Name);
        Assert.Equal(110000, response.Price);
        Assert.NotNull(response.UpdatedAt);
    }

    [Fact]
    public async Task Update_ReturnsForbid_WhenUserIsNotOwner()
    {
        using var context = CreateDbContext();

        var product = CreateProduct("seller-1");

        context.Products.Add(product);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "seller-2");

        var request = new UpdateProductRequest(
            "Changed product",
            "Description",
            1000,
            "RUB",
            "Electronics",
            true
        );

        var result = await controller.Update(product.Id, request);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal("iPhone 16 Pro", product.Name);
    }

    [Fact]
    public async Task Delete_SoftDeletesProduct_WhenCurrentUserIsOwner()
    {
        using var context = CreateDbContext();

        var product = CreateProduct("seller-1");

        context.Products.Add(product);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "seller-1");

        var result = await controller.Delete(product.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.False(product.IsActive);
        Assert.NotNull(product.UpdatedAt);

        var savedProduct = await context.Products
            .FirstAsync(p => p.Id == product.Id);

        Assert.False(savedProduct.IsActive);
    }

    [Fact]
    public async Task Delete_ReturnsForbid_WhenUserIsNotOwner()
    {
        using var context = CreateDbContext();

        var product = CreateProduct("seller-1");

        context.Products.Add(product);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "seller-2");

        var result = await controller.Delete(product.Id);

        Assert.IsType<ForbidResult>(result);
        Assert.True(product.IsActive);
    }

    [Fact]
    public async Task Admin_CanUpdateAnotherSellersProduct()
    {
        using var context = CreateDbContext();

        var product = CreateProduct("seller-1");

        context.Products.Add(product);
        await context.SaveChangesAsync();

        var controller = CreateController(
            context,
            "admin-1",
            "Admin"
        );

        var request = new UpdateProductRequest(
            "Admin updated product",
            "Updated description",
            2000,
            "RUB",
            "Electronics",
            true
        );

        var result = await controller.Update(product.Id, request);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Admin updated product", product.Name);
    }
}
