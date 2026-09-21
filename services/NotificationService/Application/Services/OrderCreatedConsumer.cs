using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Configuration;
using NotificationService.Data;
using NotificationService.Events;
using NotificationService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Services;

public class OrderCreatedConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqSettings _settings;

    public OrderCreatedConsumer(
        IServiceScopeFactory scopeFactory,
        RabbitMqSettings settings)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
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
            queue: "notification.order-created",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue: "notification.order-created",
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

            using var scope = _scopeFactory.CreateScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<NotificationDbContext>();

            dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = eventMessage.UserId,
                Type = "ORDER_CREATED",
                Title = "Order Created",
                Message =
                    $"Your order {eventMessage.OrderId} has been created successfully.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(stoppingToken);

            await channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false);
        };

        await channel.BasicConsumeAsync(
            queue: "notification.order-created",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}