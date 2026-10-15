using Microsoft.EntityFrameworkCore;
using ReportingService.Models;

namespace ReportingService.Data;

public sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    public DbSet<ReportExportJob> ExportJobs => Set<ReportExportJob>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReportExportJob>().HasIndex(x => new { x.Status, x.CreatedAt });
    }
}
