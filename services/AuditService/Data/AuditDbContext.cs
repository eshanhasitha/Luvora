using AuditService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuditService.Data;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditRecord> Records => Set<AuditRecord>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditRecord>().HasIndex(x => new { x.OccurredAt, x.Category });
        modelBuilder.Entity<AuditRecord>().HasIndex(x => x.ActorUserId);
        modelBuilder.Entity<SecurityEvent>().HasIndex(x => x.OccurredAt);
    }
}
