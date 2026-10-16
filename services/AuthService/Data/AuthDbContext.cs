using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();

    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(u => u.Id);

            entity.Property(u => u.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.LastName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.PhoneNumber).HasMaxLength(40);
            entity.Property(u => u.DisplayName).HasMaxLength(120);
            entity.Property(u => u.WardrobeCategory).HasMaxLength(20);
            entity.Property(u => u.StandardSize).HasMaxLength(30);
            entity.Property(u => u.AlterationPreference).HasMaxLength(500);
            entity.Property(u => u.PreferredFabrics).HasColumnType("text[]").IsRequired();
            entity.Property(u => u.SecondaryPhone).HasMaxLength(40);
            entity.Property(u => u.PreferredChannel).HasMaxLength(20);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasIndex(u => u.Email)
                .IsUnique();

            entity.Property(u => u.PasswordHash)
                .IsRequired();

            entity.Property(u => u.Role)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(u => u.CreatedAt)
                .IsRequired();

            entity.Property(u => u.UpdatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<UserAddress>(entity =>
        {
            entity.ToTable("user_addresses");
            entity.HasKey(address => address.Id);
            entity.Property(address => address.Label).HasMaxLength(80).IsRequired();
            entity.Property(address => address.RecipientName).HasMaxLength(200).IsRequired();
            entity.Property(address => address.PhoneNumber).HasMaxLength(40).IsRequired();
            entity.Property(address => address.AddressLine1).HasMaxLength(250).IsRequired();
            entity.Property(address => address.AddressLine2).HasMaxLength(250);
            entity.Property(address => address.City).HasMaxLength(120).IsRequired();
            entity.Property(address => address.Region).HasMaxLength(120);
            entity.Property(address => address.PostalCode).HasMaxLength(30);
            entity.Property(address => address.Country).HasMaxLength(120).IsRequired();
            entity.Property(address => address.DeliveryInstructions).HasMaxLength(500);
            entity.HasIndex(address => address.UserId);
            entity.HasOne<User>().WithMany().HasForeignKey(address => address.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.ToTable("user_preferences");
            entity.HasKey(preference => preference.UserId);
            entity.Property(preference => preference.Language).HasMaxLength(20).IsRequired();
            entity.Property(preference => preference.Currency).HasMaxLength(10).IsRequired();
            entity.Property(preference => preference.Theme).HasMaxLength(20).IsRequired();
            entity.HasOne<User>().WithOne().HasForeignKey<UserPreference>(preference => preference.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WishlistItem>(entity =>
        {
            entity.ToTable("wishlist_items");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasIndex(x => x.TokenHash)
                .IsUnique();

            entity.Property(x => x.ExpiresAt)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .IsRequired();

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.ToTable("email_verification_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
