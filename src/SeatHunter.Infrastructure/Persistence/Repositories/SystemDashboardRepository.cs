using Microsoft.EntityFrameworkCore;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Infrastructure.Persistence.Repositories;

public class SystemDashboardRepository : ISystemDashboardRepository
{
    private readonly SeatHunterDbContext _context;

    public SystemDashboardRepository(SeatHunterDbContext context)
    {
        _context = context;
    }

    public async Task<HuntsSummaryRaw> GetHuntsSummaryAsync(CancellationToken cancellationToken = default)
    {
        var statusCounts = await _context.HuntRequests
            .AsNoTracking()
            .GroupBy(h => h.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountFor(RequestStatus status) => statusCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        int active = CountFor(RequestStatus.Active);
        int completed = CountFor(RequestStatus.Completed);
        int expired = CountFor(RequestStatus.Expired);
        int cancelled = CountFor(RequestStatus.Cancelled);
        int total = statusCounts.Sum(x => x.Count);

        var activeTierCounts = await (
            from h in _context.HuntRequests.AsNoTracking()
            where h.Status == RequestStatus.Active
            join u in _context.Users.AsNoTracking() on h.UserId equals u.Id into gj
            from user in gj.DefaultIfEmpty()
            group h by (user != null ? user.Tier : UserTier.Free) into g
            select new { Tier = g.Key, Count = g.Count() }
        ).ToListAsync(cancellationToken);

        int ActiveFor(UserTier tier) => activeTierCounts.FirstOrDefault(x => x.Tier == tier)?.Count ?? 0;

        return new HuntsSummaryRaw(
            TotalHunts: total,
            ActiveHunts: active,
            CompletedHunts: completed,
            ExpiredHunts: expired,
            CancelledHunts: cancelled,
            ActiveFreeHunts: ActiveFor(UserTier.Free),
            ActiveProHunts: ActiveFor(UserTier.Pro),
            ActiveVipHunts: ActiveFor(UserTier.Vip)
        );
    }

    public async Task<UsersSummaryRaw> GetUsersSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tierCounts = await _context.Users
            .AsNoTracking()
            .GroupBy(u => u.Tier)
            .Select(g => new { Tier = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountFor(UserTier tier) => tierCounts.FirstOrDefault(x => x.Tier == tier)?.Count ?? 0;

        return new UsersSummaryRaw(
            TotalUsers: tierCounts.Sum(x => x.Count),
            FreeUsers: CountFor(UserTier.Free),
            ProUsers: CountFor(UserTier.Pro),
            VipUsers: CountFor(UserTier.Vip)
        );
    }

    public async Task<int> GetTotalPriceSnapshotsCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PriceSnapshots.CountAsync(cancellationToken);
    }
}
