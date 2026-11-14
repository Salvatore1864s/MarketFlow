using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<StockItem> StockItems => Set<StockItem>();

    public DbSet<StockReservation> StockReservations =>
        Set<StockReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StockItem>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.SellerId)
                .IsRequired()
                .HasMaxLength(450);

            entity.HasIndex(x => x.ProductId)
                .IsUnique();

            entity.HasIndex(x => x.SellerId);
        });

        modelBuilder.Entity<StockReservation>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Status)
                .HasConversion<string>();

            entity.HasIndex(x => new
                {
                    x.OrderId,
                    x.ProductId
                })
                .IsUnique();

            entity.HasIndex(x => x.ProductId);
        });
    }
}