using System.Text.Json;
using RabbitMQ.Client;

namespace Orders.Api.Messaging;

public class InventoryReservationCommandPublisher
{
    private readonly RabbitMqConnection _connection;

    public InventoryReservationCommandPublisher(
        RabbitMqConnection connection)
    {
        _connection = connection;
    }

    public Task ConfirmAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return PublishAsync(
            "inventory.confirm",
            orderId,
            cancellationToken);
    }

    public Task ReleaseAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return PublishAsync(
            "inventory.release",
            orderId,
            cancellationToken);
    }

    private async Task PublishAsync(
        string queueName,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await using var channel =
            await _connection.CreateChannelAsync(cancellationToken);

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var message = new ReservationCommandMessage
        {
            OrderId = orderId
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    private sealed class ReservationCommandMessage
    {
        public Guid OrderId { get; set; }
    }
}