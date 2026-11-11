namespace SeatHunter.Application.Interfaces;

public record HuntsSummaryRaw(
    int TotalHunts,
    int ActiveHunts,
    int CompletedHunts,
    int ExpiredHunts,
    int CancelledHunts,
    int ActiveFreeHunts,
    int ActiveProHunts,
    int ActiveVipHunts
);

public record UsersSummaryRaw(
    int TotalUsers,
    int FreeUsers,
    int ProUsers,
    int VipUsers
);

public interface ISystemDashboardRepository
{
    Task<HuntsSummaryRaw> GetHuntsSummaryAsync(CancellationToken cancellationToken = default);
    Task<UsersSummaryRaw> GetUsersSummaryAsync(CancellationToken cancellationToken = default);
    Task<int> GetTotalPriceSnapshotsCountAsync(CancellationToken cancellationToken = default);
}
