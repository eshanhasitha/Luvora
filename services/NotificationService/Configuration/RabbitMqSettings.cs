namespace NotificationService.Configuration;

public class RabbitMqSettings
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string ExchangeName { get; set; }
        = "luvora.events";
}