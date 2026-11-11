using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeatHunter.Application.Features.PriceAnalytics.Queries.GetPriceAnalytics;
using SeatHunter.Application.Features.PriceAnalytics.Queries.GetPriceHistory;
using SeatHunter.Domain.Constants;

namespace SeatHunter.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[EnableRateLimiting(AppConstants.RateLimiting.FixedPolicy)]
[Route("api/price-analytics")]
public class PriceAnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PriceAnalyticsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{routeOrEventId}")]
    public async Task<IActionResult> GetSummary([FromRoute] string routeOrEventId, CancellationToken cancellationToken)
    {
        var decodedId = System.Net.WebUtility.UrlDecode(routeOrEventId);
        var summary = await _mediator.Send(new GetPriceAnalyticsQuery(decodedId), cancellationToken);
        if (summary == null)
        {
            return NotFound(new { message = "Bu etkinlik veya rota için henüz fiyat verisi kaydedilmedi." });
        }

        return Ok(summary);
    }

    [HttpGet("{routeOrEventId}/history")]
    public async Task<IActionResult> GetHistory(
        [FromRoute] string routeOrEventId, 
        [FromQuery] int days = 30, 
        CancellationToken cancellationToken = default)
    {
        var decodedId = System.Net.WebUtility.UrlDecode(routeOrEventId);
        var history = await _mediator.Send(new GetPriceHistoryQuery(decodedId, days), cancellationToken);
        return Ok(history);
    }
}
