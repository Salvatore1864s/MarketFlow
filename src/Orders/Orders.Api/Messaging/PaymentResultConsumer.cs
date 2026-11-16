
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Enums;
using Orders.Api.Messaging.Contracts;
using Orders.Api.Models;
using Inventory.Api.Messaging.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Orders.Api.Messaging;

public class PaymentResultConsumer : BackgroundService
{
    private const string QueueName = "payment.processed";

    private readonly RabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentResultConsumer> _logger;

    public PaymentResultConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentResultConsumer> logger)
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
                var message =
                    JsonSerializer.Deserialize<PaymentProcessedMessage>(
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
                    scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

                var order = await dbContext.Orders
                    .FirstOrDefaultAsync(
                        order => order.Id == message.OrderId,
                        stoppingToken);

                if (order is null)
                {
                    _logger.LogWarning(
                        "Order {OrderId} was not found for payment result.",
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
                        "Payment result for order {OrderId} ignored. Current status: {Status}",
                        order.Id,
                        order.Status);

                    await channel.BasicAckAsync(
                        eventArgs.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);

                    return;
                }

                string eventType;

                if (message.Status == 1)
                {
                    eventType = "inventory.confirm";
                }
                else if (message.Status == 2)
                {
                    eventType = "inventory.release";
                }
                else
                {
                    _logger.LogWarning(
                        "Unknown payment status {PaymentStatus} for order {OrderId}.",
                        message.Status,
                        order.Id);

                    await channel.BasicRejectAsync(
                        eventArgs.DeliveryTag,
                        requeue: false,
                        cancellationToken: stoppingToken);

                    return;
                }

                // Проверяем, не создавалась ли уже команда
                // для этого заказа при предыдущей доставке сообщения.
                var commandAlreadyExists =
                    await dbContext.OutboxMessages.AnyAsync(
                        item =>
                            (item.EventType == "inventory.confirm" ||
                             item.EventType == "inventory.release") &&
                            item.Payload.Contains(order.Id.ToString()),
                        stoppingToken);

                if (commandAlreadyExists)
                {
                    _logger.LogInformation(
                        "Inventory command for order {OrderId} already exists in Outbox. Duplicate payment result ignored.",
                        order.Id);

                    await channel.BasicAckAsync(
                        eventArgs.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);

                    return;
                }

                var inventoryCommand = new ReservationCommandMessage
                {
                    OrderId = order.Id
                };

                var outboxMessage = new OutboxMessage
                {
                    EventType = eventType,
                    Payload = JsonSerializer.Serialize(inventoryCommand)
                };

                dbContext.OutboxMessages.Add(outboxMessage);

                await dbContext.SaveChangesAsync(stoppingToken);

                await channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                if (message.Status == 1)
                {
                    _logger.LogInformation(
                        "Inventory confirmation added to Outbox for order {OrderId}.",
                        order.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "Inventory release added to Outbox for order {OrderId}. Reason: {Reason}",
                        order.Id,
                        message.FailureReason);
                }
            }
            catch (JsonException exception)
            {
                _logger.LogError(
                    exception,
                    "Invalid payment result message.");

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
                    "Error processing payment result.");

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
            "Payment result consumer is listening to {QueueName}.",
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
