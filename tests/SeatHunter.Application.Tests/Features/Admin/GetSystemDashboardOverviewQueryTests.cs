using FluentAssertions;
using Moq;
using SeatHunter.Application.Features.Admin.Queries.GetSystemDashboardOverview;
using SeatHunter.Application.Interfaces;
using Xunit;

namespace SeatHunter.Application.Tests.Features.Admin;

public class GetSystemDashboardOverviewQueryTests
{
    private readonly Mock<ISystemDashboardRepository> _mockDashboardRepo = new();
    private readonly Mock<IProxyRotationService> _mockProxyService = new();
    private readonly Mock<IDistributedHuntScheduler> _mockScheduler = new();
    private readonly Mock<IDeadLetterReplayService> _mockReplayService = new();

    [Fact]
    public async Task Handle_ShouldAggregateAllMetricsCorrectly()
    {
        // Arrange
        var huntsRaw = new HuntsSummaryRaw(
            TotalHunts: 50,
            ActiveHunts: 20,
            CompletedHunts: 15,
            ExpiredHunts: 10,
            CancelledHunts: 5,
            ActiveFreeHunts: 8,
            ActiveProHunts: 7,
            ActiveVipHunts: 5
        );

        var usersRaw = new UsersSummaryRaw(
            TotalUsers: 100,
            FreeUsers: 70,
            ProUsers: 20,
            VipUsers: 10
        );

        _mockDashboardRepo.Setup(r => r.GetHuntsSummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(huntsRaw);
        _mockDashboardRepo.Setup(r => r.GetUsersSummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(usersRaw);
        _mockDashboardRepo.Setup(r => r.GetTotalPriceSnapshotsCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(350);

        _mockProxyService.Setup(p => p.TotalProxies).Returns(10);
        _mockProxyService.Setup(p => p.CoolingDownProxiesCount).Returns(2);

        _mockScheduler.Setup(s => s.GetScheduledCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(18);

        _mockReplayService.Setup(r => r.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeadLetterQueueStatus("hunt_requests_dlq", 4, 0, DateTime.UtcNow));

        var handler = new GetSystemDashboardOverviewQueryHandler(
            _mockDashboardRepo.Object,
            _mockProxyService.Object,
            _mockScheduler.Object,
            _mockReplayService.Object
        );

        // Act
        var result = await handler.Handle(new GetSystemDashboardOverviewQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Hunts.TotalHunts.Should().Be(50);
        result.Hunts.ActiveHunts.Should().Be(20);
        result.Hunts.ActiveVipHunts.Should().Be(5);

        result.Users.TotalUsers.Should().Be(100);
        result.Users.VipUsers.Should().Be(10);

        result.ProxyPool.TotalProxies.Should().Be(10);
        result.ProxyPool.HealthyProxies.Should().Be(8);
        result.ProxyPool.CoolingDownProxies.Should().Be(2);
        result.ProxyPool.HealthPercentage.Should().Be(80.0);

        result.Infrastructure.RedisScheduledHuntsCount.Should().Be(18);
        result.Infrastructure.DeadLetterQueueCount.Should().Be(4u);
        result.Infrastructure.TotalPriceSnapshotsCount.Should().Be(350);
    }
}
