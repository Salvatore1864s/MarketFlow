
using System.Text.Json;
using Inventory.Api.Messaging.Contracts;
using Inventory.Api.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Inventory.Api.Messaging;

public class ReservationCommandConsumer : BackgroundService
{
    private const string ConfirmQueue = "inventory.confirm";
    private const string ReleaseQueue = "inventory.release";

    private readonly RabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReservationCommandResultPublisher _publisher;
    private readonly ILogger<ReservationCommandConsumer> _logger;

    public ReservationCommandConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ReservationCommandResultPublisher publisher,
        ILogger<ReservationCommandConsumer> logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await using var confirmChannel =
            await _connection.CreateChannelAsync(stoppingToken);

        await using var releaseChannel =
            await _connection.CreateChannelAsync(stoppingToken);

        await SetupQueueAsync(confirmChannel, ConfirmQueue, stoppingToken);
        await SetupQueueAsync(releaseChannel, ReleaseQueue, stoppingToken);

        var confirmConsumer = CreateConsumer(
            confirmChannel,
            "confirm",
            stoppingToken);

        var releaseConsumer = CreateConsumer(
            releaseChannel,
            "release",
            stoppingToken);

        await confirmChannel.BasicConsumeAsync(
            queue: ConfirmQueue,
            autoAck: false,
            consumer: confirmConsumer,
            cancellationToken: stoppingToken);

        await releaseChannel.BasicConsumeAsync(
            queue: ReleaseQueue,
            autoAck: false,
            consumer: releaseConsumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Inventory reservation command consumers are listening.");

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

    private static async Task SetupQueueAsync(
        IChannel channel,
        string queueName,
        CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: cancellationToken);
    }

    private AsyncEventingBasicConsumer CreateConsumer(
        IChannel channel,
        string command,
        CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
            {
                var message =
                    JsonSerializer.Deserialize<ReservationCommandMessage>(
                        eventArgs.Body.Span);

                if (message is null || message.OrderId == Guid.Empty)
                {
                    await channel.BasicRejectAsync(
                        eventArgs.DeliveryTag,
                        requeue: false,
                        cancellationToken: stoppingToken);

                    return;
                }

                using var scope = _scopeFactory.CreateScope();

                var inventoryService =
                    scope.ServiceProvider
                        .GetRequiredService<InventoryService>();

                bool success;

                if (command == "confirm")
                {
                    success =
                        await inventoryService.ConfirmReservationAsync(
                            message.OrderId);
                }
                else
                {
                    success =
                        await inventoryService.ReleaseReservationAsync(
                            message.OrderId);
                }

                var result = new ReservationCommandResultMessage
                {
                    OrderId = message.OrderId,
                    Command = command,
                    Success = success,
                    FailureReason = success
                        ? null
                        : $"Inventory {command} operation failed."
                };

                await _publisher.PublishAsync(
                    result,
                    stoppingToken);

                await channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Inventory command {Command} processed for order {OrderId}. Success: {Success}",
                    command,
                    message.OrderId,
                    success);
            }
            catch (JsonException exception)
            {
                _logger.LogError(
                    exception,
                    "Invalid inventory reservation command.");

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
                    "Error processing inventory command {Command}.",
                    command);

                await channel.BasicNackAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: stoppingToken);
            }
        };

        return consumer;
    }
}
