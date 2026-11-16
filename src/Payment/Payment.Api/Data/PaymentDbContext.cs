using Microsoft.EntityFrameworkCore;
using Payment.Api.Models;

namespace Payment.Api.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Payment.Api.Models.Payment> Payments =>
        Set<Payment.Api.Models.Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Payment.Api.Models.Payment>(entity =>
        {
            entity.HasKey(payment => payment.Id);

            entity.HasIndex(payment => payment.OrderId)
                .IsUnique();

            entity.Property(payment => payment.Amount)
                .HasPrecision(18, 2);

            entity.Property(payment => payment.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(payment => payment.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(payment => payment.FailureReason)
                .HasMaxLength(500);
        });
    }
}