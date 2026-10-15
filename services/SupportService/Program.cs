using Microsoft.EntityFrameworkCore;
using SupportService.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers(); 
builder.Services.AddEndpointsApiExplorer(); 
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<SupportDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("SupportDatabase")));
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<SupportDbContext>().Database.MigrateAsync();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapControllers();
app.MapGet("/health", async (SupportDbContext db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "Healthy", service = "SupportService" }) : Results.StatusCode(503));
app.Run();
