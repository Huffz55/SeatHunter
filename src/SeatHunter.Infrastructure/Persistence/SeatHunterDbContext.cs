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
    }
}
