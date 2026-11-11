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
        var hunts = await _context.HuntRequests
            .AsNoTracking()
            .Select(h => new { h.Status, h.UserId })
            .ToListAsync(cancellationToken);

        int total = hunts.Count;
        int active = hunts.Count(h => h.Status == RequestStatus.Active);
        int completed = hunts.Count(h => h.Status == RequestStatus.Completed);
        int expired = hunts.Count(h => h.Status == RequestStatus.Expired);
        int cancelled = hunts.Count(h => h.Status == RequestStatus.Cancelled);

        // Aktif avların kullanıcı planlarına göre dağılımı
        var activeUserIds = hunts.Where(h => h.Status == RequestStatus.Active).Select(h => h.UserId).Distinct().ToList();
        var userTiers = await _context.Users
            .AsNoTracking()
            .Where(u => activeUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Tier, cancellationToken);

        int freeActive = 0, proActive = 0, vipActive = 0;
        foreach (var h in hunts.Where(h => h.Status == RequestStatus.Active))
        {
            if (userTiers.TryGetValue(h.UserId, out var tier))
            {
                switch (tier)
                {
                    case UserTier.Vip: vipActive++; break;
                    case UserTier.Pro: proActive++; break;
                    default: freeActive++; break;
                }
            }
            else
            {
                freeActive++;
            }
        }

        return new HuntsSummaryRaw(
            TotalHunts: total,
            ActiveHunts: active,
            CompletedHunts: completed,
            ExpiredHunts: expired,
            CancelledHunts: cancelled,
            ActiveFreeHunts: freeActive,
            ActiveProHunts: proActive,
            ActiveVipHunts: vipActive
        );
    }

    public async Task<UsersSummaryRaw> GetUsersSummaryAsync(CancellationToken cancellationToken = default)
    {
        var users = await _context.Users
            .AsNoTracking()
            .Select(u => u.Tier)
            .ToListAsync(cancellationToken);

        int total = users.Count;
        int free = users.Count(tier => tier == UserTier.Free);
        int pro = users.Count(tier => tier == UserTier.Pro);
        int vip = users.Count(tier => tier == UserTier.Vip);

        return new UsersSummaryRaw(
            TotalUsers: total,
            FreeUsers: free,
            ProUsers: pro,
            VipUsers: vip
        );
    }

    public async Task<int> GetTotalPriceSnapshotsCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PriceSnapshots.CountAsync(cancellationToken);
    }
}
