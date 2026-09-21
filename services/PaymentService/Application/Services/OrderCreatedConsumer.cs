using System.Text;
using System.Text.Json;
using PaymentService.Configuration;
using PaymentService.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PaymentService.Services;

public class OrderCreatedConsumer : BackgroundService
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(
        RabbitMqSettings settings,
        ILogger<OrderCreatedConsumer> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.Host,
            Port = _settings.Port,
            UserName = _settings.Username,
            Password = _settings.Password
        };

        await using var connection =
            await factory.CreateConnectionAsync(stoppingToken);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            exchange: _settings.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: "payment.order-created",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue: "payment.order-created",
            exchange: _settings.ExchangeName,
            routingKey: "order.created",
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var json = Encoding.UTF8.GetString(
                eventArgs.Body.ToArray());

            var eventMessage =
                JsonSerializer.Deserialize<OrderCreatedEvent>(json);

            if (eventMessage == null)
            {
                await channel.BasicNackAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: false);

                return;
            }

            _logger.LogInformation(
                "Payment received OrderCreated event. OrderId: {OrderId}, Amount: {Amount}",
                eventMessage.OrderId,
                eventMessage.TotalAmount);

            await channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false);
        };

        await channel.BasicConsumeAsync(
            queue: "payment.order-created",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(
            Timeout.InfiniteTimeSpan,
            stoppingToken);
    }
}