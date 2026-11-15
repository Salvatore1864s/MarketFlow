
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Enums;
using Orders.Api.Hubs;
using Orders.Api.Messaging.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Orders.Api.Messaging;

public class InventoryReservationCommandResultConsumer : BackgroundService
{
    private const string QueueName = "inventory.reservation-command-result";

    private readonly RabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<OrdersHub> _hubContext;
    private readonly ILogger<InventoryReservationCommandResultConsumer> _logger;

    public InventoryReservationCommandResultConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        IHubContext<OrdersHub> hubContext,
        ILogger<InventoryReservationCommandResultConsumer> logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
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
                var message =
                    JsonSerializer.Deserialize<InventoryReservationCommandResultMessage>(
                        eventArgs.Body.Span);

                if (message is null)
                {
                    await channel.BasicRejectAsync(
                        eventArgs.DeliveryTag,
                        requeue: false,
                        cancellationToken: stoppingToken);

                    return;
                }

                using var scope = _scopeFactory.CreateScope();

                var dbContext =
                    scope.ServiceProvider
                        .GetRequiredService<OrdersDbContext>();

                var order = await dbContext.Orders
                    .FirstOrDefaultAsync(
                        o => o.Id == message.OrderId,
                        stoppingToken);

                if (order is null)
                {
                    _logger.LogWarning(
                        "Order {OrderId} was not found.",
                        message.OrderId);

                    await channel.BasicAckAsync(
                        eventArgs.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);

                    return;
                }

                if (order.Status != OrderStatus.AwaitingPayment)
                {
                    _logger.LogInformation(
                        "Order {OrderId} is already processed. Status: {Status}",
                        order.Id,
                        order.Status);

                    await channel.BasicAckAsync(
                        eventArgs.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);

                    return;
                }

                if (!message.Success)
                {
                    _logger.LogError(
                        "Inventory command {Command} failed for order {OrderId}. Reason: {Reason}",
                        message.Command,
                        message.OrderId,
                        message.FailureReason);

                    await channel.BasicAckAsync(
                        eventArgs.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);

                    return;
                }

                if (string.Equals(
                    message.Command,
                    "confirm",
                    StringComparison.OrdinalIgnoreCase))
                {
                    order.Status = OrderStatus.Confirmed;
                }
                else if (string.Equals(
                    message.Command,
                    "release",
                    StringComparison.OrdinalIgnoreCase))
                {
                    order.Status = OrderStatus.Cancelled;
                }
                else
                {
                    _logger.LogWarning(
                        "Unknown inventory command {Command} for order {OrderId}.",
                        message.Command,
                        message.OrderId);

                    await channel.BasicRejectAsync(
                        eventArgs.DeliveryTag,
                        requeue: false,
                        cancellationToken: stoppingToken);

                    return;
                }

                order.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(stoppingToken);

                try
                {
                    await _hubContext.Clients
                        .Group(OrdersHub.GetBuyerGroupName(order.BuyerId))
                        .SendAsync(
                            "OrderStatusChanged",
                            new
                            {
                                orderId = order.Id,
                                status = order.Status.ToString(),
                                updatedAt = order.UpdatedAt
                            },
                            stoppingToken);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Could not send SignalR notification for order {OrderId}.",
                        order.Id);
                }

                await channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Order {OrderId} status changed to {Status}.",
                    order.Id,
                    order.Status);
            }
            catch (JsonException exception)
            {
                _logger.LogError(
                    exception,
                    "Invalid inventory command result message.");

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
                    "Error processing inventory command result.");

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
            "Orders consumer is listening to {QueueName}.",
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
            // Normal shutdown.
        }
    }
}
