using System.Text;
using System.Text.Json;
using OrderService.Configuration;
using RabbitMQ.Client;

namespace OrderService.Application.Services;

public class EventPublisher
{
    private readonly RabbitMqSettings _settings;
    private readonly RabbitMqConnectionService _connectionService;

    public EventPublisher(
        RabbitMqSettings settings,
        RabbitMqConnectionService connectionService)
    {
        _settings = settings;
        _connectionService = connectionService;
    }

    public async Task PublishAsync<T>(
    string eventName,
    T eventMessage,
    CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _connectionService.CreateConnectionAsync(cancellationToken);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: _settings.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(eventMessage));

        var properties = new BasicProperties
        {
            Persistent = true
        };

        await channel.BasicPublishAsync(
            exchange: _settings.ExchangeName,
            routingKey: eventName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}