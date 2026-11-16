using System.Text.Json;
using Orders.Api.Messaging.Contracts;
using RabbitMQ.Client;

namespace Orders.Api.Messaging;

public class PaymentCommandPublisher
{
    private const string QueueName = "payment.process";

    private readonly RabbitMqConnection _connection;

    public PaymentCommandPublisher(RabbitMqConnection connection)
    {
        _connection = connection;
    }

    public async Task PublishAsync(
        ProcessPaymentMessage message,
        CancellationToken cancellationToken = default)
    {
        await using var channel =
            await _connection.CreateChannelAsync(cancellationToken);

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: QueueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}