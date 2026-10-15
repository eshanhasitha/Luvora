using AuditService.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers(); builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AuditDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("AuditDatabase")));
builder.Services.AddHttpClient("health", client => client.Timeout = TimeSpan.FromSeconds(2));
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapControllers();
app.MapGet("/health", async (AuditDbContext db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "Healthy", service = "AuditService" }) : Results.StatusCode(503));
app.Run();
