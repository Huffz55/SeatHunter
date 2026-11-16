using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Enums;
using SeatHunter.Infrastructure.Persistence;
using SeatHunter.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SeatHunter.Infrastructure.Tests.Persistence;

public class SystemDashboardRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly SeatHunterDbContext _context;
    private readonly SystemDashboardRepository _repository;

    public SystemDashboardRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        var options = new DbContextOptionsBuilder<SeatHunterDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new SeatHunterDbContext(options);
        _repository = new SystemDashboardRepository(_context);
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetHuntsAndUsersSummaryAsync_ShouldAggregateCountsCorrectlyInDatabase()
    {
        var freeUser = new User { Id = "u-free", Email = "free@test.com", Tier = UserTier.Free };
        var proUser = new User { Id = "u-pro", Email = "pro@test.com", Tier = UserTier.Pro };
        var vipUser = new User { Id = "u-vip", Email = "vip@test.com", Tier = UserTier.Vip };

        _context.Users.AddRange(freeUser, proUser, vipUser);
        _context.HuntRequests.AddRange(
            new HuntRequest { UserId = "u-free", ProviderName = "Universal", RouteOrEventId = "https://a.com", Status = RequestStatus.Active },
            new HuntRequest { UserId = "u-pro", ProviderName = "Universal", RouteOrEventId = "https://b.com", Status = RequestStatus.Active },
            new HuntRequest { UserId = "u-vip", ProviderName = "Universal", RouteOrEventId = "https://c.com", Status = RequestStatus.Active },
            new HuntRequest { UserId = "missing-user", ProviderName = "Universal", RouteOrEventId = "https://d.com", Status = RequestStatus.Active },
            new HuntRequest { UserId = "u-vip", ProviderName = "Universal", RouteOrEventId = "https://e.com", Status = RequestStatus.Completed },
            new HuntRequest { UserId = "u-free", ProviderName = "Universal", RouteOrEventId = "https://f.com", Status = RequestStatus.Cancelled },
            new HuntRequest { UserId = "u-free", ProviderName = "Universal", RouteOrEventId = "https://g.com", Status = RequestStatus.Expired }
        );
        _context.PriceSnapshots.Add(new PriceSnapshot
        {
            RouteOrEventId = "https://a.com",
            ProviderName = "Universal",
            Price = 500m
        });
        await _context.SaveChangesAsync();

        var huntsSummary = await _repository.GetHuntsSummaryAsync();
        var usersSummary = await _repository.GetUsersSummaryAsync();
        var snapshotsCount = await _repository.GetTotalPriceSnapshotsCountAsync();

        huntsSummary.TotalHunts.Should().Be(7);
        huntsSummary.ActiveHunts.Should().Be(4);
        huntsSummary.CompletedHunts.Should().Be(1);
        huntsSummary.CancelledHunts.Should().Be(1);
        huntsSummary.ExpiredHunts.Should().Be(1);
        huntsSummary.ActiveFreeHunts.Should().Be(2);
        huntsSummary.ActiveProHunts.Should().Be(1);
        huntsSummary.ActiveVipHunts.Should().Be(1);

        usersSummary.TotalUsers.Should().Be(3);
        usersSummary.FreeUsers.Should().Be(1);
        usersSummary.ProUsers.Should().Be(1);
        usersSummary.VipUsers.Should().Be(1);

        snapshotsCount.Should().Be(1);
    }
}
