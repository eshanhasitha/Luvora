using ConfigurationService.Models;
using Microsoft.EntityFrameworkCore;

namespace ConfigurationService.Data;

public sealed class ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options) : DbContext(options)
{
    public DbSet<StoreConfiguration> Configurations => Set<StoreConfiguration>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoreConfiguration>().HasIndex(x => x.Section).IsUnique();
        modelBuilder.Entity<EmailTemplate>().HasIndex(x => x.Key).IsUnique();
    }
}
