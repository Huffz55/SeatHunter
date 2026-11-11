using MediatR;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Models;

namespace SeatHunter.Application.Features.PriceAnalytics.Queries.GetPriceAnalytics;

public record GetPriceAnalyticsQuery(string RouteOrEventId) : IRequest<PriceAnalyticsSummary?>;

public class GetPriceAnalyticsQueryHandler : IRequestHandler<GetPriceAnalyticsQuery, PriceAnalyticsSummary?>
{
    private readonly ITicketPriceHistoryService _priceHistoryService;

    public GetPriceAnalyticsQueryHandler(ITicketPriceHistoryService priceHistoryService)
    {
        _priceHistoryService = priceHistoryService;
    }

    public async Task<PriceAnalyticsSummary?> Handle(GetPriceAnalyticsQuery request, CancellationToken cancellationToken)
    {
        return await _priceHistoryService.GetPriceAnalyticsAsync(request.RouteOrEventId, cancellationToken);
    }
}
