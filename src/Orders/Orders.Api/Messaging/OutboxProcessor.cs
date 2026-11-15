
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Messaging.Contracts;
using System.Text.Json;

namespace Orders.Api.Messaging;

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InventoryCommandPublisher _publisher;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        InventoryCommandPublisher publisher,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
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
                if (outboxMessage.EventType != "inventory.reserve")
                {
                    throw new InvalidOperationException(
                        $"Unknown Outbox event type: {outboxMessage.EventType}");
                }

                var message = JsonSerializer.Deserialize<ReserveStockMessage>(
                    outboxMessage.Payload)
                    ?? throw new InvalidOperationException(
                        "Failed to deserialize Outbox message.");

                await _publisher.PublishReserveAsync(
                    message,
                    cancellationToken);

                outboxMessage.ProcessedAtUtc = DateTime.UtcNow;
                outboxMessage.AttemptCount++;
                outboxMessage.LastError = null;

                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Outbox message {MessageId} published successfully.",
                    outboxMessage.Id);
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
                    "Failed to publish Outbox message {MessageId}.",
                    outboxMessage.Id);
            }
        }
    }
}
