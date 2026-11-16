
using Catalog.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(
        DbContextOptions<CatalogDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(product => product.Id);

            entity.Property(product => product.Name)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(product => product.Description)
                .HasMaxLength(2000);

            entity.Property(product => product.Price)
                .HasPrecision(18, 2);

            entity.Property(product => product.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(product => product.Category)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(product => product.SellerId)
                .IsRequired();

            // Явно включаем URL фотографии в модель EF Core.
            entity.Property(product => product.ImageUrl)
                .HasColumnType("text")
                .IsRequired(false);

            entity.HasIndex(product => product.SellerId);
            entity.HasIndex(product => product.Category);
        });
    }
}
