
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Enums;
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
    private readonly PaymentCommandPublisher _paymentPublisher;
    private readonly ILogger<InventoryReservationResultConsumer> _logger;
    private readonly IConfiguration _configuration;

    public InventoryReservationResultConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        PaymentCommandPublisher paymentPublisher,
        ILogger<InventoryReservationResultConsumer> logger,
        IConfiguration configuration)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _paymentPublisher = paymentPublisher;
        _logger = logger;
        _configuration = configuration;
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

                await _paymentPublisher.PublishAsync(
                    new ProcessPaymentMessage
                    {
                        OrderId = order.Id,
                        BuyerId = order.BuyerId,
                        Amount = order.TotalAmount,
                        Currency = "RUB",
                        SimulateFailure = _configuration.GetValue<bool>(
                            "Payment:SimulateFailure")
                    },
                    stoppingToken);

                order.Status = OrderStatus.AwaitingPayment;
                order.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(stoppingToken);

                await channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Payment command published for order {OrderId}.",
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
}
