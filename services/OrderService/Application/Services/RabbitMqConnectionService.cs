using OrderService.Configuration;
using RabbitMQ.Client;

namespace OrderService.Application.Services;

public class RabbitMqConnectionService
{
    private readonly RabbitMqSettings _settings;

    public RabbitMqConnectionService(
        RabbitMqSettings settings)
    {
        _settings = settings;
    }

    public async Task<IConnection> CreateConnectionAsync(
    CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.Host,
            Port = _settings.Port,
            UserName = _settings.Username,
            Password = _settings.Password
        };

        return await factory.CreateConnectionAsync(cancellationToken);
    }
}