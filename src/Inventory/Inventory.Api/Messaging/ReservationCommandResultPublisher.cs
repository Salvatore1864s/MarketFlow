using System.Text.Json;
using Inventory.Api.Messaging.Contracts;
using RabbitMQ.Client;

namespace Inventory.Api.Messaging;

public class ReservationCommandResultPublisher
{
    private const string QueueName = "inventory.reservation-command-result";

    private readonly RabbitMqConnection _connection;

    public ReservationCommandResultPublisher(
        RabbitMqConnection connection)
    {
        _connection = connection;
    }

    public async Task PublishAsync(
        ReservationCommandResultMessage message,
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