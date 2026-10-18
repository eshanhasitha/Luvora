using Microsoft.EntityFrameworkCore;
using ReportingService.Data;
using ReportingService.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers(); builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<ReportingDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("ReportingDatabase")));
builder.Services.AddHttpClient("reports", client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<ReportDataProvider>();
builder.Services.AddHostedService<ReportExportWorker>();
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<ReportingDbContext>().Database.MigrateAsync();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapControllers();
app.MapGet("/health", async (ReportingDbContext db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "Healthy", service = "ReportingService" }) : Results.StatusCode(503));
app.Run();
