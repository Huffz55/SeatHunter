using MediatR;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;

namespace SeatHunter.Application.Features.PriceAnalytics.Queries.GetPriceHistory;

public record PriceSnapshotDto(
    Guid Id,
    string RouteOrEventId,
    string ProviderName,
    string? EventName,
    string? CategoryName,
    decimal Price,
    string Currency,
    DateTime RecordedAt
);

public record GetPriceHistoryQuery(string RouteOrEventId, int Days = 30) : IRequest<IReadOnlyList<PriceSnapshotDto>>;

public class GetPriceHistoryQueryHandler : IRequestHandler<GetPriceHistoryQuery, IReadOnlyList<PriceSnapshotDto>>
{
    private readonly ITicketPriceHistoryService _priceHistoryService;

    public GetPriceHistoryQueryHandler(ITicketPriceHistoryService priceHistoryService)
    {
        _priceHistoryService = priceHistoryService;
    }

    public async Task<IReadOnlyList<PriceSnapshotDto>> Handle(GetPriceHistoryQuery request, CancellationToken cancellationToken)
    {
        var snapshots = await _priceHistoryService.GetPriceHistoryAsync(request.RouteOrEventId, request.Days, cancellationToken);
        return snapshots.Select(s => new PriceSnapshotDto(
            s.Id,
            s.RouteOrEventId,
            s.ProviderName,
            s.EventName,
            s.CategoryName,
            s.Price,
            s.Currency,
            s.RecordedAt
        )).ToList();
    }
}
