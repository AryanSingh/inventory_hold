using System.Text;
using System.Text.Json;
using Contracts;
using Domain.Interfaces;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public class RabbitMqPublisher : IEventPublisher, IAsyncDisposable, IDisposable
{
    private readonly string _connectionString;
    private readonly Lazy<Task<(IConnection Connection, IChannel Channel)>> _lazy;
    private bool _disposed;
    private const string ExchangeName = "hold.events";

    public RabbitMqPublisher(string connectionString)
    {
        _connectionString = connectionString;
        _lazy = new Lazy<Task<(IConnection, IChannel)>>(InitializeAsync);
    }

    private async Task<(IConnection Connection, IChannel Channel)> InitializeAsync()
    {
        var factory = new ConnectionFactory { Uri = new Uri(_connectionString) };
        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Fanout, durable: true);
        return (connection, channel);
    }

    private async Task<IChannel> GetChannelAsync()
    {
        var (_, channel) = await _lazy.Value;
        return channel;
    }

    public async Task PublishHoldCreatedAsync(string holdId, List<HoldItemDto> items, DateTime createdAt, DateTime expiresAt)
    {
        var payload = new { holdId, items, createdAt, expiresAt };
        await PublishAsync("hold.created", "HoldCreated", payload);
    }

    public async Task PublishHoldReleasedAsync(string holdId, List<HoldItemDto> items, DateTime releasedAt)
    {
        var payload = new { holdId, items, releasedAt };
        await PublishAsync("hold.released", "HoldReleased", payload);
    }

    public async Task PublishHoldExpiredAsync(string holdId, List<HoldItemDto> items, DateTime expiredAt)
    {
        var payload = new { holdId, items, expiredAt };
        await PublishAsync("hold.expired", "HoldExpired", payload);
    }

    private async Task PublishAsync(string routingKey, string eventType, object payload)
    {
        var channel = await GetChannelAsync();
        var json = JsonSerializer.Serialize(payload);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Headers = new Dictionary<string, object?> { { "event_type", eventType } },
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_lazy.IsValueCreated)
        {
            try
            {
                var (connection, channel) = await _lazy.Value;
                if (channel != null) await channel.CloseAsync();
                if (connection != null) await connection.CloseAsync();
            }
            catch
            {
                // Best-effort cleanup during shutdown
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_lazy.IsValueCreated && _lazy.IsValueCreated)
        {
            try
            {
                var (connection, channel) = _lazy.Value.GetAwaiter().GetResult();
                channel?.Dispose();
                connection?.Dispose();
            }
            catch
            {
                // Best-effort cleanup during shutdown
            }
        }
    }
}
