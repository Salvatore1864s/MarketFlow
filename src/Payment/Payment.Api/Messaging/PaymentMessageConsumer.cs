
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Payment.Api.Messaging.Contracts;
using Payment.Api.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Payment.Api.Messaging;

public class PaymentMessageConsumer : BackgroundService
{
    private const string QueueName = "payment.process";

    private readonly RabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentMessageConsumer> _logger;

    public PaymentMessageConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentMessageConsumer> logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await using var channel = await _connection.CreateChannelAsync(
            stoppingToken);

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
                var json = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

                var message = JsonSerializer.Deserialize<ProcessPaymentMessage>(json);

                if (message is null)
                {
                    throw new InvalidOperationException(
                        "Received an empty payment message.");
                }

                using var scope = _scopeFactory.CreateScope();

                var paymentService = scope.ServiceProvider
                    .GetRequiredService<PaymentService>();

                var publisher = scope.ServiceProvider
                    .GetRequiredService<PaymentEventPublisher>();

                var request = new Payment.Api.DTOs.ProcessPaymentRequest
                {
                    OrderId = message.OrderId,
                    BuyerId = message.BuyerId,
                    Amount = message.Amount,
                    Currency = message.Currency
                };

                var payment = await paymentService.ProcessAsync(
                    request,
                    simulateFailure: message.SimulateFailure,
                    cancellationToken: stoppingToken);

                var result = new PaymentProcessedMessage
                {
                    PaymentId = payment.Id,
                    OrderId = payment.OrderId,
                    BuyerId = payment.BuyerId,
                    Amount = payment.Amount,
                    Currency = payment.Currency,
                    Status = payment.Status,
                    FailureReason = payment.FailureReason,
                    ProcessedAt = DateTime.UtcNow
                };

                await publisher.PublishAsync(result, stoppingToken);

                await channel.BasicAckAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Payment processed for order {OrderId}. Status: {Status}",
                    payment.OrderId,
                    payment.Status);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to process payment message.");

                await channel.BasicNackAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Payment consumer is listening to queue {QueueName}.",
            QueueName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
