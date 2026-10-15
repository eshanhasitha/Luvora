using Microsoft.EntityFrameworkCore;
using PromotionService.Data;
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
var cartUrl = builder.Configuration["Services:CartService"] ?? "http://localhost:5253/";
builder.Services.AddHttpClient("carts", client => client.BaseAddress = new Uri(cartUrl));
builder.Services.AddDbContext<PromotionDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("PromotionDatabase")));
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<PromotionDbContext>().Database.MigrateAsync();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseAuthentication(); app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", async (PromotionDbContext db) => await db.Database.CanConnectAsync()
    ? Results.Ok(new { status = "Healthy", service = "PromotionService" })
    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.Run();
