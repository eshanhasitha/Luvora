using Microsoft.EntityFrameworkCore;
using ReportingService.Data;

namespace ReportingService.Services;

public sealed class ReportExportWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<ReportExportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
                var job = await db.ExportJobs.Where(x => x.Status == "Queued").OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(stoppingToken);
                if (job is null) { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); continue; }
                job.Status = "Running"; job.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(stoppingToken);
                try
                {
                    var provider = scope.ServiceProvider.GetRequiredService<ReportDataProvider>();
                    var result = await provider.GetAsync(job.ReportType, job.From, job.To, stoppingToken);
                    var folder = configuration["Exports:Path"] ?? Path.Combine(AppContext.BaseDirectory, "exports");
                    Directory.CreateDirectory(folder);
                    var file = $"{job.Id:N}.json";
                    await File.WriteAllTextAsync(Path.Combine(folder, file), result.GetRawText(), stoppingToken);
                    job.Status = "Completed"; job.FileName = file; job.DownloadExpiresAt = DateTime.UtcNow.AddMinutes(15); job.UpdatedAt = DateTime.UtcNow;
                }
                catch (Exception exception) when (exception is HttpRequestException or NotSupportedException or IOException)
                {
                    job.Status = "Failed"; job.Error = exception.Message; job.UpdatedAt = DateTime.UtcNow;
                    logger.LogWarning("Report export {ExportId} failed: {Reason}", job.Id, exception.Message);
                }
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Report export worker iteration failed."); await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
        }
    }
}
