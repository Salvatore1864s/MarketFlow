
using System.Data;
using Inventory.Api.Clients;
using Inventory.Api.Data;
using Inventory.Api.DTOs;
using Inventory.Api.Enums;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Services;

public class InventoryService
{
    private readonly InventoryDbContext _dbContext;
    private readonly CatalogClient _catalogClient;

    public InventoryService(
        InventoryDbContext dbContext,
        CatalogClient catalogClient)
    {
        _dbContext = dbContext;
        _catalogClient = catalogClient;
    }

    public async Task<StockResponse?> GetStockAsync(Guid productId)
    {
        var stock = await _dbContext.StockItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProductId == productId);

        return stock is null ? null : ToResponse(stock);
    }

    public async Task<StockResponse> CreateStockAsync(
        CreateStockRequest request,
        string sellerId,
        bool isAdmin)
    {
        var product = await _catalogClient.GetProductAsync(
            request.ProductId);

        if (product is null)
        {
            throw new KeyNotFoundException(
                "Product not found in Catalog.");
        }

        if (!isAdmin && product.SellerId != sellerId)
        {
            throw new UnauthorizedAccessException(
                "You cannot manage stock for another seller's product.");
        }

        var existingStock = await _dbContext.StockItems
            .FirstOrDefaultAsync(x => x.ProductId == request.ProductId);

        if (existingStock is not null)
        {
            throw new InvalidOperationException(
                "Stock for this product already exists.");
        }

        var stock = new StockItem
        {
            ProductId = request.ProductId,
            SellerId = product.SellerId,
            AvailableQuantity = request.Quantity,
            ReservedQuantity = 0,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.StockItems.Add(stock);
        await _dbContext.SaveChangesAsync();

        return ToResponse(stock);
    }

    public async Task<bool> ReserveStockIdempotentlyAsync(
        ReserveStockRequest request)
    {
        if (request.OrderId == Guid.Empty ||
            request.Items.Count == 0)
        {
            return false;
        }

        var existingReservations = await _dbContext.StockReservations
            .AsNoTracking()
            .Where(x => x.OrderId == request.OrderId)
            .OrderBy(x => x.ProductId)
            .ToListAsync();

        if (existingReservations.Count > 0)
        {
            if (existingReservations.Any(
                x => x.Status != ReservationStatus.Pending))
            {
                return false;
            }

            var requestedItems = request.Items
                .OrderBy(x => x.ProductId)
                .ToList();

            if (existingReservations.Count != requestedItems.Count)
            {
                return false;
            }

            for (var i = 0; i < requestedItems.Count; i++)
            {
                if (existingReservations[i].ProductId !=
                        requestedItems[i].ProductId ||
                    existingReservations[i].Quantity !=
                        requestedItems[i].Quantity)
                {
                    return false;
                }
            }

            return true;
        }

        return await ReserveStockAsync(request);
    }

    public async Task<bool> ReserveStockAsync(
        ReserveStockRequest request)
    {
        if (request.OrderId == Guid.Empty ||
            request.Items.Count == 0)
        {
            return false;
        }

        if (request.Items.Any(x =>
            x.ProductId == Guid.Empty || x.Quantity <= 0))
        {
            return false;
        }

        var productIds = request.Items
            .Select(x => x.ProductId)
            .ToList();

        if (productIds.Distinct().Count() != productIds.Count)
        {
            return false;
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var existingReservation = await _dbContext.StockReservations
            .AnyAsync(x => x.OrderId == request.OrderId);

        if (existingReservation)
        {
            await transaction.RollbackAsync();
            return false;
        }

        var stocks = await _dbContext.StockItems
            .Where(x => productIds.Contains(x.ProductId))
            .OrderBy(x => x.ProductId)
            .ToListAsync();

        foreach (var item in request.Items)
        {
            var stock = stocks.FirstOrDefault(
                x => x.ProductId == item.ProductId);

            if (stock is null)
            {
                await transaction.RollbackAsync();
                return false;
            }

            var inStock =
                stock.AvailableQuantity - stock.ReservedQuantity;

            if (inStock < item.Quantity)
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        foreach (var item in request.Items)
        {
            var stock = stocks.First(
                x => x.ProductId == item.ProductId);

            stock.ReservedQuantity += item.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;

            _dbContext.StockReservations.Add(new StockReservation
            {
                OrderId = request.OrderId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                Status = ReservationStatus.Pending
            });
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return true;
    }

    public async Task<bool> ConfirmReservationAsync(Guid orderId)
    {
        if (orderId == Guid.Empty)
            return false;

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var reservations = await _dbContext.StockReservations
            .Where(x => x.OrderId == orderId)
            .OrderBy(x => x.ProductId)
            .ToListAsync();

        if (reservations.Count == 0)
        {
            await transaction.RollbackAsync();
            return false;
        }

        // Повторная команда подтверждения.
        if (reservations.All(
            x => x.Status == ReservationStatus.Confirmed))
        {
            await transaction.CommitAsync();
            return true;
        }

        if (reservations.Any(
            x => x.Status != ReservationStatus.Pending))
        {
            await transaction.RollbackAsync();
            return false;
        }

        foreach (var reservation in reservations)
        {
            var stock = await _dbContext.StockItems
                .FirstOrDefaultAsync(
                    x => x.ProductId == reservation.ProductId);

            if (stock is null ||
                stock.ReservedQuantity < reservation.Quantity ||
                stock.AvailableQuantity < reservation.Quantity)
            {
                await transaction.RollbackAsync();
                return false;
            }

            stock.AvailableQuantity -= reservation.Quantity;
            stock.ReservedQuantity -= reservation.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;

            reservation.Status = ReservationStatus.Confirmed;
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return true;
    }

    public async Task<bool> ReleaseReservationAsync(Guid orderId)
    {
        if (orderId == Guid.Empty)
            return false;

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var reservations = await _dbContext.StockReservations
            .Where(x => x.OrderId == orderId)
            .OrderBy(x => x.ProductId)
            .ToListAsync();

        if (reservations.Count == 0)
        {
            await transaction.RollbackAsync();
            return false;
        }

        // Повторная команда освобождения.
        if (reservations.All(
            x => x.Status == ReservationStatus.Released))
        {
            await transaction.CommitAsync();
            return true;
        }

        if (reservations.Any(
            x => x.Status != ReservationStatus.Pending))
        {
            await transaction.RollbackAsync();
            return false;
        }

        foreach (var reservation in reservations)
        {
            var stock = await _dbContext.StockItems
                .FirstOrDefaultAsync(
                    x => x.ProductId == reservation.ProductId);

            if (stock is null ||
                stock.ReservedQuantity < reservation.Quantity)
            {
                await transaction.RollbackAsync();
                return false;
            }

            stock.ReservedQuantity -= reservation.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;

            reservation.Status = ReservationStatus.Released;
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return true;
    }

    private static StockResponse ToResponse(StockItem stock)
    {
        return new StockResponse(
            stock.ProductId,
            stock.SellerId,
            stock.AvailableQuantity,
            stock.ReservedQuantity,
            stock.AvailableQuantity - stock.ReservedQuantity
        );
    }
}
