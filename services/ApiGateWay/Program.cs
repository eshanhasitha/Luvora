using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        RoleClaimType = ClaimTypes.Role,
        ClockSkew = TimeSpan.FromSeconds(30)
    });
builder.Services.AddAuthorization(options => options.AddPolicy("AdminOnly", policy =>
    policy.RequireAuthenticatedUser().RequireRole("Admin")));
builder.Services.AddHttpClient("audit", client => client.Timeout = TimeSpan.FromSeconds(2));

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

app.UseAuthentication();
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var isAdminRequest = path.StartsWith("/api/admin/", StringComparison.OrdinalIgnoreCase);
    try
    {
        await next();
    }
    catch
    {
        if (isAdminRequest) context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        throw;
    }
    finally
    {
        var auditBase = builder.Configuration["Audit:BaseUrl"];
        var auditKey = builder.Configuration["Audit:IngestKey"];
        if (isAdminRequest && Uri.TryCreate(auditBase, UriKind.Absolute, out var baseAddress) && !string.IsNullOrWhiteSpace(auditKey))
        {
            try
            {
                var idValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                Guid? actorId = Guid.TryParse(idValue, out var parsedId) ? parsedId : null;
                var actorRole = string.Join(',', context.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value));
                var status = context.Response.StatusCode;
                var category = path.Contains("/exports", StringComparison.OrdinalIgnoreCase) ? "export"
                    : path.EndsWith("/login", StringComparison.OrdinalIgnoreCase) || status is 401 or 403 ? "security" : "admin";
                var client = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("audit");
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseAddress, "api/internal/audit"));
                request.Headers.Add("X-Audit-Key", auditKey);
                request.Content = JsonContent.Create(new
                {
                    ActorUserId = actorId,
                    ActorRole = actorRole,
                    Action = context.Request.Method,
                    Resource = path,
                    HttpMethod = context.Request.Method,
                    ResponseStatus = status,
                    CorrelationId = context.TraceIdentifier,
                    Category = category
                });
                using var ignored = await client.SendAsync(request, CancellationToken.None);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                app.Logger.LogWarning("Could not write admin audit event: {Reason}", exception.Message);
            }
        }
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
