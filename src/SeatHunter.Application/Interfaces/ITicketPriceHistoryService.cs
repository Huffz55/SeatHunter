using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Models;

namespace SeatHunter.Application.Interfaces;

public record PriceEvaluationResult(
    PriceSnapshot Snapshot,
    bool IsPriceDrop,
    decimal? PreviousPrice,
    decimal? DropPercentage,
    PriceAnalyticsSummary? Summary
);

public interface ITicketPriceHistoryService
{
    Task<PriceEvaluationResult> RecordPriceObservationAsync(
        string routeOrEventId,
        string providerName,
        string? eventName,
        decimal observedPrice,
        string? categoryName = null,
        decimal? userMaxPrice = null,
        CancellationToken cancellationToken = default);

    Task<PriceAnalyticsSummary?> GetPriceAnalyticsAsync(
        string routeOrEventId, 
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PriceSnapshot>> GetPriceHistoryAsync(
        string routeOrEventId, 
        int days = 30, 
        CancellationToken cancellationToken = default);
}
