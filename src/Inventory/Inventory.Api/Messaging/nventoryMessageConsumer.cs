
using System.Text.Json;
using Inventory.Api.DTOs;
using Inventory.Api.Messaging.Contracts;
using Inventory.Api.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Inventory.Api.Messaging;

public class InventoryMessageConsumer : BackgroundService
{
    private const string QueueName = "inventory.reserve";

    private readonly RabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InventoryMessageConsumer> _logger;

    public InventoryMessageConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<InventoryMessageConsumer> logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await using var channel =
            await _connection.CreateChannelAsync(stoppingToken);

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<ReserveStockMessage>(
                    eventArgs.Body.Span);

                if (message is null)
                {
                    _logger.LogWarning(
                        "Received empty reservation message.");

                    await channel.BasicRejectAsync(
                        eventArgs.DeliveryTag,
                        requeue: false,
                        cancellationToken: stoppingToken);

                    return;
                }

                using var scope = _scopeFactory.CreateScope();

                var inventoryService =
                    scope.ServiceProvider.GetRequiredService<InventoryService>();

                var publisher =
                    scope.ServiceProvider.GetRequiredService
                        <ReservationResultPublisher>();

                var request = new ReserveStockRequest
                {
                    OrderId = message.OrderId,
                    Items = message.Items.Select(item =>
                        new ReserveStockItemRequest
                        {
                            ProductId = item.ProductId,
                            Quantity = item.Quantity
                        }).ToList()
                };

                var success =
                    await inventoryService.ReserveStockIdempotentlyAsync(
                        request);

                var result = new ReservationResultMessage
                {
                    OrderId = message.OrderId,
                    Success = success,
                    FailureReason = success
                        ? null
                        : "Stock reservation failed."
                };

                await publisher.PublishAsync(
                    result,
                    stoppingToken);

                await channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Reservation processed for order {OrderId}. Success: {Success}",
                    message.OrderId,
                    success);
            }
            catch (JsonException exception)
            {
                _logger.LogError(
                    exception,
                    "Invalid reservation message received.");

                await channel.BasicRejectAsync(
                    eventArgs.DeliveryTag,
                    requeue: false,
                    cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                // Application is shutting down.
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error processing inventory reservation message.");

                await channel.BasicNackAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Inventory consumer is listening to queue {QueueName}.",
            QueueName);

        try
        {
            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
    }
}
