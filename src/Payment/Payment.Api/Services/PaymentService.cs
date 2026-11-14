
using Microsoft.EntityFrameworkCore;
using Payment.Api.Data;
using Payment.Api.DTOs;
using Payment.Api.Models;

namespace Payment.Api.Services;

public class PaymentService
{
    private readonly PaymentDbContext _dbContext;

    public PaymentService(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaymentResponse> ProcessAsync(
        ProcessPaymentRequest request,
        bool simulateFailure = false,
        CancellationToken cancellationToken = default)
    {
        if (request.OrderId == Guid.Empty)
            throw new ArgumentException("OrderId is required.");

        if (string.IsNullOrWhiteSpace(request.BuyerId))
            throw new ArgumentException("BuyerId is required.");

        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        var currency = request.Currency?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must contain exactly 3 characters.");

        var existingPayment = await _dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                payment => payment.OrderId == request.OrderId,
                cancellationToken);

        if (existingPayment is not null)
        {
            return ReturnExistingOrThrow(
                existingPayment,
                request,
                currency);
        }

        var now = DateTime.UtcNow;

        var payment = new Payment.Api.Models.Payment
        {
            OrderId = request.OrderId,
            BuyerId = request.BuyerId,
            Amount = request.Amount,
            Currency = currency,
            Status = simulateFailure
                ? PaymentStatus.Failed
                : PaymentStatus.Succeeded,
            CreatedAt = now,
            CompletedAt = now,
            FailureReason = simulateFailure
                ? "Payment was declined by the payment simulator."
                : null
        };

        _dbContext.Payments.Add(payment);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(payment).State = EntityState.Detached;

            var concurrentPayment = await _dbContext.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.OrderId == request.OrderId,
                    cancellationToken);

            if (concurrentPayment is null)
                throw;

            return ReturnExistingOrThrow(
                concurrentPayment,
                request,
                currency);
        }

        return MapToResponse(payment);
    }

    public async Task<PaymentResponse?> GetByOrderAsync(
        Guid orderId,
        string buyerId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                payment => payment.OrderId == orderId &&
                           payment.BuyerId == buyerId,
                cancellationToken);

        return payment is null ? null : MapToResponse(payment);
    }

    private static PaymentResponse ReturnExistingOrThrow(
        Payment.Api.Models.Payment existingPayment,
        ProcessPaymentRequest request,
        string currency)
    {
        if (existingPayment.BuyerId != request.BuyerId ||
            existingPayment.Amount != request.Amount ||
            existingPayment.Currency != currency)
        {
            throw new InvalidOperationException(
                "A payment already exists for this order with different details.");
        }

        return MapToResponse(existingPayment);
    }

    private static PaymentResponse MapToResponse(
        Payment.Api.Models.Payment payment)
    {
        return new PaymentResponse
        {
            Id = payment.Id,
            OrderId = payment.OrderId,
            BuyerId = payment.BuyerId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status,
            CreatedAt = payment.CreatedAt,
            CompletedAt = payment.CompletedAt,
            FailureReason = payment.FailureReason
        };
    }
}
