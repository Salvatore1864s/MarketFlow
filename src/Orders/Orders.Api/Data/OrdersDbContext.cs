
using Microsoft.EntityFrameworkCore;
using Orders.Api.Models;

namespace Orders.Api.Data;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.BuyerId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(o => o.Status)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(o => o.TotalAmount)
                .HasPrecision(18, 2);

            entity.HasIndex(o => o.BuyerId);

            entity.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(i => i.Id);

            entity.Property(i => i.ProductName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(i => i.UnitPrice)
                .HasPrecision(18, 2);

            entity.Property(i => i.LineTotal)
                .HasPrecision(18, 2);

            entity.HasIndex(i => i.ProductId);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(message => message.Id);

            entity.Property(message => message.EventType)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(message => message.Payload)
                .IsRequired();

            entity.HasIndex(message => message.ProcessedAtUtc);

            entity.HasIndex(message => message.CreatedAtUtc);
        });
    }
}
