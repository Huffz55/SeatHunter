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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<HuntRequest>().HasKey(x => x.Id);
        modelBuilder.Entity<User>().HasKey(x => x.Id);
        modelBuilder.Entity<Route>().HasKey(x => x.Id);
        modelBuilder.Entity<Ticket>().HasKey(x => x.Id);

        // Performans İndeksleri
        modelBuilder.Entity<HuntRequest>()
            .HasIndex(x => new { x.Status, x.NextScanTime })
            .HasDatabaseName("IX_HuntRequests_Status_NextScanTime");

        modelBuilder.Entity<HuntRequest>()
            .HasIndex(x => x.UserId)
            .HasDatabaseName("IX_HuntRequests_UserId");

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
