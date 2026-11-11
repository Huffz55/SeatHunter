using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Models;

namespace SeatHunter.Infrastructure.Services;

public class TicketPriceHistoryService : ITicketPriceHistoryService
{
    private readonly IPriceHistoryRepository _repository;
    private readonly ILogger<TicketPriceHistoryService> _logger;

    public TicketPriceHistoryService(
        IPriceHistoryRepository repository, 
        ILogger<TicketPriceHistoryService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<PriceEvaluationResult> RecordPriceObservationAsync(
        string routeOrEventId, 
        string providerName, 
        string? eventName, 
        decimal observedPrice, 
        string? categoryName = null, 
        decimal? userMaxPrice = null, 
        CancellationToken cancellationToken = default)
    {
        var previousSnapshot = await _repository.GetLatestSnapshotAsync(routeOrEventId, cancellationToken);

        var snapshot = new PriceSnapshot
        {
            Id = Guid.NewGuid(),
            RouteOrEventId = routeOrEventId,
            ProviderName = providerName,
            EventName = eventName,
            CategoryName = categoryName,
            Price = observedPrice,
            Currency = "TRY",
            RecordedAt = DateTime.UtcNow
        };

        await _repository.AddSnapshotAsync(snapshot, cancellationToken);
        _logger.LogInformation("📊 Fiyat gözlemi kaydedildi: {Route} - {Price} TL ({Provider})", 
            routeOrEventId, observedPrice, providerName);

        var summary = await _repository.GetAnalyticsSummaryAsync(routeOrEventId, cancellationToken);

        bool isPriceDrop = false;
        decimal? dropPercentage = null;
        decimal? prevPrice = previousSnapshot?.Price;

        if (previousSnapshot != null && observedPrice < previousSnapshot.Price)
        {
            isPriceDrop = true;
            dropPercentage = Math.Round(((previousSnapshot.Price - observedPrice) / previousSnapshot.Price) * 100, 2);
            _logger.LogInformation("📉 Fiyat düşüşü tespit edildi! Önceki: {Prev} TL, Yeni: {Current} TL (Düşüş: %{Drop})",
                previousSnapshot.Price, observedPrice, dropPercentage);
        }
        else if (userMaxPrice.HasValue && observedPrice <= userMaxPrice.Value && previousSnapshot != null && previousSnapshot.Price > userMaxPrice.Value)
        {
            isPriceDrop = true;
            dropPercentage = Math.Round(((previousSnapshot.Price - observedPrice) / previousSnapshot.Price) * 100, 2);
            _logger.LogInformation("🎯 Hedef bütçe altına inildi! Maks Bütçe: {Max} TL, Yeni Fiyat: {Price} TL",
                userMaxPrice.Value, observedPrice);
        }

        return new PriceEvaluationResult(
            Snapshot: snapshot,
            IsPriceDrop: isPriceDrop,
            PreviousPrice: prevPrice,
            DropPercentage: dropPercentage,
            Summary: summary
        );
    }

    public async Task<PriceAnalyticsSummary?> GetPriceAnalyticsAsync(
        string routeOrEventId, 
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetAnalyticsSummaryAsync(routeOrEventId, cancellationToken);
    }

    public async Task<IReadOnlyList<PriceSnapshot>> GetPriceHistoryAsync(
        string routeOrEventId, 
        int days = 30, 
        CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        return await _repository.GetHistoryAsync(routeOrEventId, since, cancellationToken);
    }
}
