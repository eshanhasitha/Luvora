var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddControllers();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
    try
    {
        await next();
    }
    finally
    {
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startedAt);
        app.Logger.LogInformation(
            "{Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            elapsed.TotalMilliseconds);
    }
});

app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () =>
    Results.Ok(new
    {
        status = "Healthy",
        service = "ApiGateway"
    }));

app.MapReverseProxy();

app.Run();
