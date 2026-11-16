
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Messaging.Contracts;
using Orders.Api.Models;
using Inventory.Api.Messaging.Contracts;

namespace Orders.Api.Messaging;

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InventoryCommandPublisher _inventoryPublisher;
    private readonly PaymentCommandPublisher _paymentPublisher;
    private readonly InventoryReservationCommandPublisher _reservationPublisher;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        InventoryCommandPublisher inventoryPublisher,
        PaymentCommandPublisher paymentPublisher,
        InventoryReservationCommandPublisher reservationPublisher,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _inventoryPublisher = inventoryPublisher;
        _paymentPublisher = paymentPublisher;
        _reservationPublisher = reservationPublisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error while processing Outbox messages.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(5),
                stoppingToken);
        }
    }

    private async Task ProcessMessagesAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<OrdersDbContext>();

        var messages = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAtUtc == null)
            .OrderBy(message => message.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var outboxMessage in messages)
        {
            try
            {
                await PublishMessageAsync(
                    outboxMessage,
                    cancellationToken);

                outboxMessage.ProcessedAtUtc = DateTime.UtcNow;
                outboxMessage.AttemptCount++;
                outboxMessage.LastError = null;

                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Outbox message {MessageId} of type {EventType} published successfully.",
                    outboxMessage.Id,
                    outboxMessage.EventType);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                outboxMessage.AttemptCount++;
                outboxMessage.LastError = exception.Message;

                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogError(
                    exception,
                    "Failed to publish Outbox message {MessageId} of type {EventType}.",
                    outboxMessage.Id,
                    outboxMessage.EventType);
            }
        }
    }

    private async Task PublishMessageAsync(
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken)
    {
        switch (outboxMessage.EventType)
        {
            case "inventory.reserve":
            {
                var message =
                    JsonSerializer.Deserialize<ReserveStockMessage>(
                        outboxMessage.Payload)
                    ?? throw new InvalidOperationException(
                        "Failed to deserialize inventory.reserve message.");

                await _inventoryPublisher.PublishReserveAsync(
                    message,
                    cancellationToken);

                break;
            }

            case "payment.process":
            {
                var message =
                    JsonSerializer.Deserialize<ProcessPaymentMessage>(
                        outboxMessage.Payload)
                    ?? throw new InvalidOperationException(
                        "Failed to deserialize payment.process message.");

                await _paymentPublisher.PublishAsync(
                    message,
                    cancellationToken);

                break;
            }

            case "inventory.confirm":
            {
                var message =
                    JsonSerializer.Deserialize<ReservationCommandMessage>(
                        outboxMessage.Payload)
                    ?? throw new InvalidOperationException(
                        "Failed to deserialize inventory.confirm message.");

                await _reservationPublisher.ConfirmAsync(
                    message.OrderId,
                    cancellationToken);

                break;
            }

            case "inventory.release":
            {
                var message =
                    JsonSerializer.Deserialize<ReservationCommandMessage>(
                        outboxMessage.Payload)
                    ?? throw new InvalidOperationException(
                        "Failed to deserialize inventory.release message.");

                await _reservationPublisher.ReleaseAsync(
                    message.OrderId,
                    cancellationToken);

                break;
            }

            default:
                throw new InvalidOperationException(
                    $"Unknown Outbox event type: {outboxMessage.EventType}");
        }
    }
}
