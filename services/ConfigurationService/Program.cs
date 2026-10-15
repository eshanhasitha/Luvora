using ConfigurationService.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers(); builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ConfigurationDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("ConfigurationDatabase")));
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>().Database.MigrateAsync();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapControllers();
app.MapGet("/health", async (ConfigurationDbContext db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "Healthy", service = "ConfigurationService" }) : Results.StatusCode(503));
app.Run();
