using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Models;

namespace SeatHunter.Application.Interfaces;

public interface IPriceHistoryRepository
{
    Task AddSnapshotAsync(PriceSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PriceSnapshot>> GetHistoryAsync(string routeOrEventId, DateTime? since = null, CancellationToken cancellationToken = default);
    Task<PriceAnalyticsSummary?> GetAnalyticsSummaryAsync(string routeOrEventId, CancellationToken cancellationToken = default);
    Task<PriceSnapshot?> GetLatestSnapshotAsync(string routeOrEventId, CancellationToken cancellationToken = default);
}
