
using System.Text;
using System.Text.Json;
using Inventory.Api.Messaging.Contracts;
using RabbitMQ.Client;

namespace Inventory.Api.Messaging;

public class ReservationResultPublisher
{
    private const string QueueName = "inventory.reservation-result";

    private readonly RabbitMqConnection _connection;

    public ReservationResultPublisher(RabbitMqConnection connection)
    {
        _connection = connection;
    }

    public async Task PublishAsync(
        ReservationResultMessage message,
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

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
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
