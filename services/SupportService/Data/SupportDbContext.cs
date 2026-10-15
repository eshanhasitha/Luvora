using Microsoft.EntityFrameworkCore;
using SupportService.Models;

namespace SupportService.Data;

public sealed class SupportDbContext(DbContextOptions<SupportDbContext> options) : DbContext(options)
{
    public DbSet<SupportTicket> Tickets => Set<SupportTicket>();
    public DbSet<TicketMessage> Messages => Set<TicketMessage>();
    public DbSet<Complaint> Complaints => Set<Complaint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SupportTicket>().HasIndex(x => new { x.Status, x.CreatedAt });
        modelBuilder.Entity<TicketMessage>().HasOne<SupportTicket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Complaint>().HasIndex(x => new { x.Status, x.CreatedAt });
    }
}
