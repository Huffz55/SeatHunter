using Microsoft.EntityFrameworkCore;
using SeatHunter.Domain.Entities;

namespace SeatHunter.Infrastructure.Persistence;

public class SeatHunterDbContext : DbContext
{
    public SeatHunterDbContext(DbContextOptions<SeatHunterDbContext> options) : base(options) { }

    public DbSet<HuntRequest> HuntRequests { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Route> Routes { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<PriceSnapshot> PriceSnapshots { get; set; }
    public DbSet<UserNotification> Notifications { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<HuntRequest>().HasKey(x => x.Id);
        modelBuilder.Entity<User>().HasKey(x => x.Id);
        modelBuilder.Entity<Route>().HasKey(x => x.Id);
        modelBuilder.Entity<Ticket>().HasKey(x => x.Id);
        modelBuilder.Entity<PriceSnapshot>().HasKey(x => x.Id);
        modelBuilder.Entity<UserNotification>().HasKey(x => x.Id);
        modelBuilder.Entity<Subscription>().HasKey(x => x.Id);
        modelBuilder.Entity<PaymentTransaction>().HasKey(x => x.Id);

        modelBuilder.Entity<Subscription>()
            .HasIndex(x => new { x.UserId, x.Status })
            .HasDatabaseName("IX_Subscriptions_UserId_Status");

        modelBuilder.Entity<Subscription>()
            .HasIndex(x => new { x.Status, x.CurrentPeriodEndUtc })
            .HasDatabaseName("IX_Subscriptions_Status_PeriodEnd");

        modelBuilder.Entity<PaymentTransaction>()
            .HasIndex(x => x.ExternalSessionId)
            .IsUnique()
            .HasDatabaseName("IX_PaymentTransactions_ExternalSessionId");

        modelBuilder.Entity<PaymentTransaction>()
            .HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_PaymentTransactions_IdempotencyKey");

        modelBuilder.Entity<PaymentTransaction>()
            .HasIndex(x => new { x.UserId, x.CreatedAtUtc })
            .HasDatabaseName("IX_PaymentTransactions_UserId_CreatedAt");

        modelBuilder.Entity<UserNotification>()
            .HasIndex(x => new { x.UserId, x.CreatedAt })
            .HasDatabaseName("IX_Notifications_UserId_CreatedAt");

        modelBuilder.Entity<UserNotification>()
            .HasIndex(x => new { x.UserId, x.IsRead })
            .HasDatabaseName("IX_Notifications_UserId_IsRead");

        // Performans İndeksleri
        modelBuilder.Entity<HuntRequest>()
            .HasIndex(x => new { x.Status, x.NextScanTime })
            .HasDatabaseName("IX_HuntRequests_Status_NextScanTime");

        modelBuilder.Entity<HuntRequest>()
            .HasIndex(x => x.UserId)
            .HasDatabaseName("IX_HuntRequests_UserId");

        modelBuilder.Entity<PriceSnapshot>()
            .HasIndex(x => new { x.RouteOrEventId, x.RecordedAt })
            .HasDatabaseName("IX_PriceSnapshots_RouteOrEventId_RecordedAt");

        // Benzersiz Kullanıcı E-posta İndeksi
        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        modelBuilder.Entity<User>()
            .Property(x => x.Tier)
            .HasDefaultValue(Domain.Enums.UserTier.Free);
    }
}
