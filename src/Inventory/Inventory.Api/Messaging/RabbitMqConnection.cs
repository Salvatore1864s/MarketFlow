
using RabbitMQ.Client;

namespace Inventory.Api.Messaging;

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
        var host = configuration["RabbitMQ:Host"]
            ?? throw new InvalidOperationException("RabbitMQ Host not found.");

        var port = int.Parse(
            configuration["RabbitMQ:Port"]
            ?? throw new InvalidOperationException("RabbitMQ Port not found."));

        var userName = configuration["RabbitMQ:UserName"]
            ?? throw new InvalidOperationException("RabbitMQ UserName not found.");

        var password = configuration["RabbitMQ:Password"]
            ?? throw new InvalidOperationException("RabbitMQ Password not found.");

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = userName,
            Password = password
        };

        var connection = await factory.CreateConnectionAsync(cancellationToken);

        return new RabbitMqConnection(connection);
    }

    public async Task<IChannel> CreateChannelAsync(
        CancellationToken cancellationToken = default)
    {
        return await _connection.CreateChannelAsync(
            cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
