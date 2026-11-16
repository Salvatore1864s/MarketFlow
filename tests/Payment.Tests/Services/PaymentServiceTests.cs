
using Microsoft.EntityFrameworkCore;
using Payment.Api.Data;
using Payment.Api.DTOs;
using Payment.Api.Models;
using Payment.Api.Services;

namespace Payment.Tests.Services;

public class PaymentServiceTests
{
    private static (PaymentDbContext Context, PaymentService Service) CreateService()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new PaymentDbContext(options);
        var service = new PaymentService(context);

        return (context, service);
    }

    private static ProcessPaymentRequest CreateRequest(
        Guid? orderId = null,
        string buyerId = "buyer-123",
        decimal amount = 1500m,
        string currency = "RUB")
    {
        return new ProcessPaymentRequest
        {
            OrderId = orderId ?? Guid.NewGuid(),
            BuyerId = buyerId,
            Amount = amount,
            Currency = currency
        };
    }

    [Fact]
    public async Task ProcessAsync_ValidRequest_CreatesSuccessfulPayment()
    {
        var (context, service) = CreateService();
        var request = CreateRequest();

        var result = await service.ProcessAsync(request);

        Assert.Equal(request.OrderId, result.OrderId);
        Assert.Equal(request.BuyerId, result.BuyerId);
        Assert.Equal(request.Amount, result.Amount);
        Assert.Equal(PaymentStatus.Succeeded, result.Status);
        Assert.Null(result.FailureReason);

        Assert.Equal(1, await context.Payments.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_SimulateFailure_CreatesFailedPayment()
    {
        var (context, service) = CreateService();
        var request = CreateRequest();

        var result = await service.ProcessAsync(
            request,
            simulateFailure: true);

        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.NotNull(result.FailureReason);
        Assert.NotNull(result.CompletedAt);

        Assert.Equal(1, await context.Payments.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_SameRequestTwice_ReturnsExistingPayment()
    {
        var (context, service) = CreateService();
        var request = CreateRequest();

        var firstResult = await service.ProcessAsync(request);
        var secondResult = await service.ProcessAsync(request);

        Assert.Equal(firstResult.Id, secondResult.Id);
        Assert.Equal(1, await context.Payments.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_SameOrderWithDifferentAmount_ThrowsException()
    {
        var (_, service) = CreateService();
        var orderId = Guid.NewGuid();

        var firstRequest = CreateRequest(
            orderId: orderId,
            amount: 1500m);

        var secondRequest = CreateRequest(
            orderId: orderId,
            amount: 2000m);

        await service.ProcessAsync(firstRequest);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ProcessAsync(secondRequest));
    }

    [Fact]
    public async Task ProcessAsync_EmptyOrderId_ThrowsException()
    {
        var (_, service) = CreateService();
        var request = CreateRequest(orderId: Guid.Empty);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ProcessAsync(request));
    }

    [Fact]
    public async Task ProcessAsync_EmptyBuyerId_ThrowsException()
    {
        var (_, service) = CreateService();
        var request = CreateRequest(buyerId: " ");

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ProcessAsync(request));
    }

    [Fact]
    public async Task ProcessAsync_NonPositiveAmount_ThrowsException()
    {
        var (_, service) = CreateService();
        var request = CreateRequest(amount: 0);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ProcessAsync(request));
    }

    [Fact]
    public async Task ProcessAsync_InvalidCurrency_ThrowsException()
    {
        var (_, service) = CreateService();
        var request = CreateRequest(currency: "RU");

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ProcessAsync(request));
    }

    [Fact]
    public async Task GetByOrderAsync_ExistingPayment_ReturnsPayment()
    {
        var (_, service) = CreateService();
        var request = CreateRequest();

        await service.ProcessAsync(request);

        var result = await service.GetByOrderAsync(
            request.OrderId,
            request.BuyerId);

        Assert.NotNull(result);
        Assert.Equal(request.OrderId, result.OrderId);
        Assert.Equal(request.BuyerId, result.BuyerId);
    }

    [Fact]
    public async Task GetByOrderAsync_WrongBuyer_ReturnsNull()
    {
        var (_, service) = CreateService();
        var request = CreateRequest();

        await service.ProcessAsync(request);

        var result = await service.GetByOrderAsync(
            request.OrderId,
            "another-buyer");

        Assert.Null(result);
    }
}
