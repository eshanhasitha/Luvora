using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Configuration;
using OrderService.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(
            "OrderDatabase")
    ));

builder.Services.Configure<RabbitMqSettings>(
    builder.Configuration.GetSection("RabbitMq"));

var rabbitMqSettings =
    builder.Configuration
        .GetSection("RabbitMq")
        .Get<RabbitMqSettings>()
    ?? new RabbitMqSettings();

builder.Services.AddSingleton(rabbitMqSettings);

builder.Services.AddSingleton<RabbitMqConnectionService>();
builder.Services.AddSingleton<EventPublisher>();



var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();