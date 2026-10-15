using Microsoft.EntityFrameworkCore;
using PromotionService.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<PromotionDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("PromotionDatabase")));
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<PromotionDbContext>().Database.MigrateAsync();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapControllers();
app.MapGet("/health", async (PromotionDbContext db) => await db.Database.CanConnectAsync()
    ? Results.Ok(new { status = "Healthy", service = "PromotionService" })
    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.Run();
