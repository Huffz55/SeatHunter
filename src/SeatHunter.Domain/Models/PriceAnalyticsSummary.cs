namespace SeatHunter.Domain.Models;

public record PriceAnalyticsSummary(
    string RouteOrEventId,
    decimal CurrentPrice,
    decimal MinPrice,
    decimal MaxPrice,
    decimal AveragePrice,
    decimal PriceDropPercentage,
    int TotalSnapshotsCount,
    DateTime FirstRecordedAt,
    DateTime LastRecordedAt
);
