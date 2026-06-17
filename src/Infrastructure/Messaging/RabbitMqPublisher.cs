using System.Text;
using System.Text.Json;
using Contracts;
using Domain.Interfaces;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public class RabbitMqPublisher : IEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private const string ExchangeName = "hold.events";

    public RabbitMqPublisher(IConnection connection)
    {
        _connection = connection;
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        _channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Fanout, durable: true).GetAwaiter().GetResult();
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
        var json = JsonSerializer.Serialize(payload);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Headers = new Dictionary<string, object?> { { "event_type", eventType } },
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await _channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
