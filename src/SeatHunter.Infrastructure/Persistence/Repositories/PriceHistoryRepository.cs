using Microsoft.EntityFrameworkCore;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Models;

namespace SeatHunter.Infrastructure.Persistence.Repositories;

public class PriceHistoryRepository : IPriceHistoryRepository
{
    private readonly SeatHunterDbContext _context;

    public PriceHistoryRepository(SeatHunterDbContext context)
    {
        _context = context;
    }

    public async Task AddSnapshotAsync(PriceSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _context.PriceSnapshots.AddAsync(snapshot, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PriceSnapshot>> GetHistoryAsync(
        string routeOrEventId, 
        DateTime? since = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.PriceSnapshots
            .AsNoTracking()
            .Where(p => p.RouteOrEventId == routeOrEventId);

        if (since.HasValue)
        {
            query = query.Where(p => p.RecordedAt >= since.Value);
        }

        return await query
            .OrderBy(p => p.RecordedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PriceSnapshot?> GetLatestSnapshotAsync(
        string routeOrEventId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.PriceSnapshots
            .AsNoTracking()
            .Where(p => p.RouteOrEventId == routeOrEventId)
            .OrderByDescending(p => p.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PriceAnalyticsSummary?> GetAnalyticsSummaryAsync(
        string routeOrEventId, 
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _context.PriceSnapshots
            .AsNoTracking()
            .Where(p => p.RouteOrEventId == routeOrEventId)
            .OrderBy(p => p.RecordedAt)
            .ToListAsync(cancellationToken);

        if (snapshots.Count == 0)
        {
            return null;
        }

        var latest = snapshots[^1];
        var minPrice = snapshots.Min(p => p.Price);
        var maxPrice = snapshots.Max(p => p.Price);
        var avgPrice = Math.Round(snapshots.Average(p => p.Price), 2);
        var currentPrice = latest.Price;

        decimal dropPercentage = 0;
        if (maxPrice > 0 && currentPrice < maxPrice)
        {
            dropPercentage = Math.Round(((maxPrice - currentPrice) / maxPrice) * 100, 2);
        }

        return new PriceAnalyticsSummary(
            RouteOrEventId: routeOrEventId,
            CurrentPrice: currentPrice,
            MinPrice: minPrice,
            MaxPrice: maxPrice,
            AveragePrice: avgPrice,
            PriceDropPercentage: dropPercentage,
            TotalSnapshotsCount: snapshots.Count,
            FirstRecordedAt: snapshots[0].RecordedAt,
            LastRecordedAt: latest.RecordedAt
        );
    }
}
