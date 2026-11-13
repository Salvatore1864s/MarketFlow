
using System.Security.Claims;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly CatalogDbContext _dbContext;

    public ProductsController(CatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Получить все активные товары
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .OrderByDescending(product => product.CreatedAt)
            .Select(product => new ProductResponse(
                product.Id,
                product.Name,
                product.Description,
                product.Price,
                product.Currency,
                product.Category,
                product.SellerId,
                product.CreatedAt,
                product.UpdatedAt,
                product.IsActive
            ))
            .ToListAsync();

        return Ok(products);
    }

    // Получить один товар
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(product =>
                product.Id == id && product.IsActive);

        if (product == null)
        {
            return NotFound();
        }

        return Ok(ToResponse(product));
    }

    // Создать товар
    [Authorize(Roles = "Seller,Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateProductRequest request)
    {
        var sellerId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(sellerId))
        {
            return Unauthorized();
        }

        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Currency = request.Currency,
            Category = request.Category,
            SellerId = sellerId
        };

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            ToResponse(product)
        );
    }

    // Изменить товар
    [Authorize(Roles = "Seller,Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateProductRequest request)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(product => product.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        if (!CanManageProduct(product))
        {
            return Forbid();
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.Currency = request.Currency;
        product.Category = request.Category;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(ToResponse(product));
    }

    // Удалить товар
    [Authorize(Roles = "Seller,Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(product => product.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        if (!CanManageProduct(product))
        {
            return Forbid();
        }

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    private bool CanManageProduct(Product product)
    {
        if (User.IsInRole("Admin"))
        {
            return true;
        }

        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return userId == product.SellerId;
    }

    private static ProductResponse ToResponse(Product product)
    {
        return new ProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Currency,
            product.Category,
            product.SellerId,
            product.CreatedAt,
            product.UpdatedAt,
            product.IsActive
        );
    }
}
