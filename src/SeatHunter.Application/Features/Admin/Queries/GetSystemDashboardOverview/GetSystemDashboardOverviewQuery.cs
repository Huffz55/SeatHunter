using MediatR;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Application.Features.Admin.Queries.GetSystemDashboardOverview;

public record HuntsOverviewDto(
    int TotalHunts,
    int ActiveHunts,
    int CompletedHunts,
    int ExpiredHunts,
    int CancelledHunts,
    int ActiveFreeHunts,
    int ActiveProHunts,
    int ActiveVipHunts
);

public record UsersOverviewDto(
    int TotalUsers,
    int FreeUsers,
    int ProUsers,
    int VipUsers
);

public record ProxyPoolOverviewDto(
    int TotalProxies,
    int HealthyProxies,
    int CoolingDownProxies,
    double HealthPercentage
);

public record InfrastructureOverviewDto(
    long RedisScheduledHuntsCount,
    uint DeadLetterQueueCount,
    int TotalPriceSnapshotsCount
);

public record SystemDashboardOverviewDto(
    HuntsOverviewDto Hunts,
    UsersOverviewDto Users,
    ProxyPoolOverviewDto ProxyPool,
    InfrastructureOverviewDto Infrastructure,
    DateTime GeneratedAt
);

public record GetSystemDashboardOverviewQuery : IRequest<SystemDashboardOverviewDto>;

public class GetSystemDashboardOverviewQueryHandler : IRequestHandler<GetSystemDashboardOverviewQuery, SystemDashboardOverviewDto>
{
    private readonly ISystemDashboardRepository _dashboardRepository;
    private readonly IProxyRotationService _proxyService;
    private readonly IDistributedHuntScheduler _scheduler;
    private readonly IDeadLetterReplayService _replayService;

    public GetSystemDashboardOverviewQueryHandler(
        ISystemDashboardRepository dashboardRepository,
        IProxyRotationService proxyService,
        IDistributedHuntScheduler scheduler,
        IDeadLetterReplayService replayService)
    {
        _dashboardRepository = dashboardRepository;
        _proxyService = proxyService;
        _scheduler = scheduler;
        _replayService = replayService;
    }

    public async Task<SystemDashboardOverviewDto> Handle(GetSystemDashboardOverviewQuery request, CancellationToken cancellationToken)
    {
        var huntsRaw = await _dashboardRepository.GetHuntsSummaryAsync(cancellationToken);
        var usersRaw = await _dashboardRepository.GetUsersSummaryAsync(cancellationToken);
        var priceSnapshotsCount = await _dashboardRepository.GetTotalPriceSnapshotsCountAsync(cancellationToken);
        var scheduledCount = await _scheduler.GetScheduledCountAsync(cancellationToken);
        var dlqStatus = await _replayService.GetStatusAsync(cancellationToken);

        // Proxy havuz metrikleri
        int totalProxies = _proxyService.TotalProxies;
        int coolingDown = _proxyService.CoolingDownProxiesCount;
        int healthy = Math.Max(0, totalProxies - coolingDown);
        double healthPercent = totalProxies > 0 ? Math.Round((double)healthy / totalProxies * 100, 1) : 100.0;

        return new SystemDashboardOverviewDto(
            Hunts: new HuntsOverviewDto(
                huntsRaw.TotalHunts,
                huntsRaw.ActiveHunts,
                huntsRaw.CompletedHunts,
                huntsRaw.ExpiredHunts,
                huntsRaw.CancelledHunts,
                huntsRaw.ActiveFreeHunts,
                huntsRaw.ActiveProHunts,
                huntsRaw.ActiveVipHunts
            ),
            Users: new UsersOverviewDto(
                usersRaw.TotalUsers,
                usersRaw.FreeUsers,
                usersRaw.ProUsers,
                usersRaw.VipUsers
            ),
            ProxyPool: new ProxyPoolOverviewDto(
                TotalProxies: totalProxies,
                HealthyProxies: healthy,
                CoolingDownProxies: coolingDown,
                HealthPercentage: healthPercent
            ),
            Infrastructure: new InfrastructureOverviewDto(
                RedisScheduledHuntsCount: scheduledCount,
                DeadLetterQueueCount: dlqStatus.MessageCount,
                TotalPriceSnapshotsCount: priceSnapshotsCount
            ),
            GeneratedAt: DateTime.UtcNow
        );
    }
}
