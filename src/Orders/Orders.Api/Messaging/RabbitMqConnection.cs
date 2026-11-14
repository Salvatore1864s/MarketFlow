
using RabbitMQ.Client;

namespace Orders.Api.Messaging;

public class RabbitMqConnection : IAsyncDisposable
{
    private readonly IConnection _connection;

    private RabbitMqConnection(IConnection connection)
    {
        _connection = connection;
    }

    public static async Task<RabbitMqConnection> CreateAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.TryParse(configuration["RabbitMQ:Port"], out var port)
                ? port
                : 5672,
            UserName = configuration["RabbitMQ:UserName"]
                ?? throw new InvalidOperationException(
                    "RabbitMQ username is not configured."),
            Password = configuration["RabbitMQ:Password"]
                ?? throw new InvalidOperationException(
                    "RabbitMQ password is not configured.")
        };

        var connection = await factory.CreateConnectionAsync(cancellationToken);

        return new RabbitMqConnection(connection);
    }

    public Task<IChannel> CreateChannelAsync(
        CancellationToken cancellationToken = default)
    {
        return _connection.CreateChannelAsync(
            cancellationToken: cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _connection.DisposeAsync();
    }
}
