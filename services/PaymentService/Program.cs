using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Configuration;
using PaymentService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT audience is not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
    ValidIssuer = jwtIssuer, ValidAudience = jwtAudience, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
    RoleClaimType = System.Security.Claims.ClaimTypes.Role, ClockSkew = TimeSpan.FromSeconds(30)
});
builder.Services.AddAuthorization();
var orderServiceUrl = builder.Configuration["Services:OrderService"] ?? "http://localhost:5046/";
builder.Services.AddHttpClient("orders", client => client.BaseAddress = new Uri(orderServiceUrl));

var rabbitMqSettings =
    builder.Configuration
        .GetSection("RabbitMq")
        .Get<RabbitMqSettings>()
    ?? new RabbitMqSettings();

builder.Services.AddSingleton(rabbitMqSettings);

builder.Services.AddHostedService<OrderCreatedConsumer>();

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(
            "PaymentDatabase")
    ));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("HttpsRedirection:Enabled"))
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
