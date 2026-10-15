using DeliveryService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;

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

builder.Services.AddDbContext<DeliveryDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(
            "DeliveryDatabase")
    ));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
