
using System.Text;
using System.Text.Json;
using Orders.Api.Messaging.Contracts;
using RabbitMQ.Client;

namespace Orders.Api.Messaging;

public class InventoryCommandPublisher
{
    private const string QueueName = "inventory.reserve";

    private readonly RabbitMqConnection _connection;

    public InventoryCommandPublisher(RabbitMqConnection connection)
    {
        _connection = connection;
    }

    public async Task PublishReserveAsync(
        ReserveStockMessage message,
        CancellationToken cancellationToken = default)
    {
        await using var channel = await _connection.CreateChannelAsync(
            cancellationToken);

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
