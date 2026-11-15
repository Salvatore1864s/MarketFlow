
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Enums;
using Orders.Api.Hubs;
using Orders.Api.Messaging.Contracts;
using Orders.Api.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Orders.Api.Messaging;

public class InventoryReservationResultConsumer : BackgroundService
{
    private const string QueueName = "inventory.reservation-result";

    private readonly RabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InventoryReservationResultConsumer> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHubContext<OrdersHub> _hubContext;

    public InventoryReservationResultConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<InventoryReservationResultConsumer> logger,
        IConfiguration configuration,
        IHubContext<OrdersHub> hubContext)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
        _hubContext = hubContext;
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
                    JsonSerializer.Deserialize<ReservationResultMessage>(
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
                    .Include(o => o.Items)
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

                if (order.Status != OrderStatus.Pending)
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
                    order.Status = OrderStatus.Cancelled;
                    order.UpdatedAt = DateTime.UtcNow;

                    await dbContext.SaveChangesAsync(stoppingToken);

                    await SendStatusNotificationAsync(
                        order,
                        stoppingToken);

                    _logger.LogWarning(
                        "Order {OrderId} cancelled. Reason: {Reason}",
                        order.Id,
                        message.FailureReason);

                    await channel.BasicAckAsync(
                        eventArgs.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);

                    return;
                }

                var paymentMessage = new ProcessPaymentMessage
                {
                    OrderId = order.Id,
                    BuyerId = order.BuyerId,
                    Amount = order.TotalAmount,
                    Currency = "RUB",
                    SimulateFailure = _configuration.GetValue<bool>(
                        "Payment:SimulateFailure")
                };

                var outboxMessage = new OutboxMessage
                {
                    EventType = "payment.process",
                    Payload = JsonSerializer.Serialize(paymentMessage)
                };

                dbContext.OutboxMessages.Add(outboxMessage);

                order.Status = OrderStatus.AwaitingPayment;
                order.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(stoppingToken);

                await SendStatusNotificationAsync(
                    order,
                    stoppingToken);

                await channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Payment command added to Outbox for order {OrderId}.",
                    order.Id);
            }
            catch (JsonException exception)
            {
                _logger.LogError(
                    exception,
                    "Invalid inventory reservation result message.");

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
                    "Error processing inventory reservation result.");

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

    private async Task SendStatusNotificationAsync(
        Orders.Api.Models.Order order,
        CancellationToken cancellationToken)
    {
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
                    cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not send SignalR notification for order {OrderId}.",
                order.Id);
        }
    }
}
