using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using AuthService.Data;
using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public sealed class EmailVerificationService(
    AuthDbContext db,
    IConfiguration configuration,
    ILogger<EmailVerificationService> logger)
{
    public async Task<bool> SendAsync(User user, CancellationToken cancellationToken = default)
    {
        var host = configuration["Email:Host"];
        var from = configuration["Email:From"];
        var verificationUrl = configuration["Email:VerificationUrl"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(verificationUrl))
            return false;

        var now = DateTime.UtcNow;
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var storedTokens = await db.EmailVerificationTokens.Where(x => x.UserId == user.Id && x.UsedAt == null).ToListAsync(cancellationToken);
        db.EmailVerificationTokens.RemoveRange(storedTokens);
        db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = Hash(rawToken),
            CreatedAt = now, ExpiresAt = now.AddHours(24)
        });
        await db.SaveChangesAsync(cancellationToken);

        var separator = verificationUrl.Contains('?') ? "&" : "?";
        var link = $"{verificationUrl}{separator}token={Uri.EscapeDataString(rawToken)}";
        using var message = new MailMessage(from, user.Email)
        {
            Subject = "Verify your Luvora email address",
            Body = $"Hello {user.FirstName},\r\n\r\nVerify your email address within 24 hours using this link:\r\n{link}\r\n\r\nIf you did not create this account, you can ignore this message.",
            IsBodyHtml = false
        };
        var port = int.TryParse(configuration["Email:Port"], out var configuredPort) ? configuredPort : 587;
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = bool.TryParse(configuration["Email:UseSsl"], out var useSsl) && useSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000
        };
        var username = configuration["Email:Username"];
        var password = configuration["Email:Password"];
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password);

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            logger.LogError(exception, "Could not send an email verification message for user {UserId}.", user.Id);
            var tokenHash = Hash(rawToken);
            var token = await db.EmailVerificationTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
            if (token is not null) db.EmailVerificationTokens.Remove(token);
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }
    }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
