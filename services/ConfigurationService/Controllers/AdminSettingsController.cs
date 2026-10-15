using System.Text.Json;
using ConfigurationService.Data;
using ConfigurationService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConfigurationService.Controllers;

[ApiController]
[Route("api/admin/settings")]
public sealed class AdminSettingsController(ConfigurationDbContext db) : ControllerBase
{
    private static readonly HashSet<string> Sections = new(StringComparer.OrdinalIgnoreCase) { "general", "currency", "tax", "shipping", "payments", "notifications", "homepage" };

    [HttpGet("{section}")]
    public async Task<IActionResult> GetSection(string section)
    {
        if (!Sections.Contains(section)) return NotFound();
        if (section.Equals("payments", StringComparison.OrdinalIgnoreCase))
        {
            var payment = await db.Configurations.AsNoTracking().SingleOrDefaultAsync(x => x.Section == "payments");
            var info = payment is null ? new PaymentSettings() : JsonSerializer.Deserialize<PaymentSettings>(payment.ValuesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new PaymentSettings();
            return Ok(new { providerName = info.ProviderName, enabled = info.Enabled, configured = !string.IsNullOrWhiteSpace(info.ProviderName) });
        }
        var item = await db.Configurations.AsNoTracking().SingleOrDefaultAsync(x => x.Section == section.ToLowerInvariant());
        return item is null ? Ok(new { section = section.ToLowerInvariant(), values = new Dictionary<string, JsonElement>() })
            : Ok(new { section = item.Section, values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(item.ValuesJson) ?? [] });
    }

    [HttpPut("{section}")]
    public async Task<IActionResult> UpdateSection(string section, ValuesRequest request)
    {
        section = section.ToLowerInvariant();
        if (!Sections.Contains(section)) return NotFound();
        if (request.Values is null || request.Values.Count == 0) return BadRequest(new { message = "At least one setting is required." });
        if (section == "payments")
        {
            if (request.Values.Keys.Any(ContainsSecretName)) return BadRequest(new { message = "Provider secrets must be configured through server-side secret management." });
            var safe = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            if (request.Values.TryGetValue("providerName", out var name)) safe["providerName"] = name;
            if (request.Values.TryGetValue("enabled", out var enabled)) safe["enabled"] = enabled;
            if (safe.Count != request.Values.Count) return BadRequest(new { message = "Only providerName and enabled are configurable through this API." });
        }
        if (request.Values.Any(x => ContainsSecretName(x.Key) || ContainsSecretValue(x.Value))) return BadRequest(new { message = "Secret values must be configured through server-side secret management." });
        var row = await db.Configurations.SingleOrDefaultAsync(x => x.Section == section);
        if (row is null) { row = new StoreConfiguration { Id = Guid.NewGuid(), Section = section }; db.Configurations.Add(row); }
        row.ValuesJson = JsonSerializer.Serialize(request.Values); row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { section, values = section == "payments" ? null : request.Values, updatedAt = row.UpdatedAt });
    }

    [HttpGet("email-templates")]
    public async Task<IActionResult> ListTemplates() => Ok(await db.EmailTemplates.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Key, x.Name, x.Subject, x.IsActive, x.UpdatedAt }).ToListAsync());

    [HttpGet("email-templates/{templateId:guid}")]
    public async Task<IActionResult> GetTemplate(Guid templateId)
    {
        var item = await db.EmailTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == templateId);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("email-templates/{templateId:guid}")]
    public async Task<IActionResult> UpdateTemplate(Guid templateId, EmailTemplateRequest request)
    {
        var item = await db.EmailTemplates.SingleOrDefaultAsync(x => x.Id == templateId);
        if (item is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.HtmlBody)) return BadRequest(new { message = "Subject and body are required." });
        item.Subject = request.Subject.Trim(); item.HtmlBody = request.HtmlBody; item.IsActive = request.IsActive; item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); return Ok(item);
    }

    [HttpPost("email-templates/{templateId:guid}/test")]
    public async Task<IActionResult> TestTemplate(Guid templateId, TestEmailRequest request)
    {
        if (!await db.EmailTemplates.AnyAsync(x => x.Id == templateId)) return NotFound();
        if (!System.Net.Mail.MailAddress.TryCreate(request.Email, out _)) return BadRequest(new { message = "A valid recipient email is required." });
        return Problem(statusCode: StatusCodes.Status501NotImplemented, title: "Email delivery is not configured.");
    }

    private static bool ContainsSecretName(string name)
    {
        var normalized = name.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        return normalized.Contains("secret") || normalized.Contains("password") || normalized.Contains("privatekey") || normalized.Contains("apikey") || normalized.Contains("token") || normalized.Contains("encryptionkey");
    }

    private static bool ContainsSecretValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
            return value.EnumerateObject().Any(property => ContainsSecretName(property.Name) || ContainsSecretValue(property.Value));
        if (value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().Any(ContainsSecretValue);
        return false;
    }

    public sealed record ValuesRequest(Dictionary<string, JsonElement> Values);
    public sealed record EmailTemplateRequest(string Subject, string HtmlBody, bool IsActive);
    public sealed record TestEmailRequest(string Email);
    public sealed record PaymentSettings(string? ProviderName = null, bool Enabled = false);
}
