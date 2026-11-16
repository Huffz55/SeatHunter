using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeatHunter.Application.Features.PriceAnalytics.Queries.GetPriceAnalytics;
using SeatHunter.Application.Features.PriceAnalytics.Queries.GetPriceHistory;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Constants;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting(AppConstants.RateLimiting.FixedPolicy)]
[Route("api/price-analytics")]
public class PriceAnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserRepository _userRepository;
    private readonly IHuntRequestRepository _huntRequestRepository;

    public PriceAnalyticsController(
        IMediator mediator,
        IUserRepository userRepository,
        IHuntRequestRepository huntRequestRepository)
    {
        _mediator = mediator;
        _userRepository = userRepository;
        _huntRequestRepository = huntRequestRepository;
    }

    [HttpGet("{routeOrEventId}")]
    public async Task<IActionResult> GetSummary([FromRoute] string routeOrEventId, CancellationToken cancellationToken)
    {
        var decodedId = System.Net.WebUtility.UrlDecode(routeOrEventId);
        var denial = await AuthorizePriceAccessAsync(decodedId, cancellationToken);
        if (denial != null) return denial;

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
        var denial = await AuthorizePriceAccessAsync(decodedId, cancellationToken);
        if (denial != null) return denial;

        var history = await _mediator.Send(new GetPriceHistoryQuery(decodedId, days), cancellationToken);
        return Ok(history);
    }

    private async Task<IActionResult?> AuthorizePriceAccessAsync(string routeOrEventId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin")) return null;

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null || !user.IsActive) return Unauthorized();

        if (user.GetEffectiveTier() == UserTier.Free)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Fiyat analizi özelliği yalnızca Pro ve VIP planlara özeldir."
            });
        }

        var hasTrackedRoute = await _huntRequestRepository.UserHasHuntForRouteAsync(userId, routeOrEventId, cancellationToken);
        if (!hasTrackedRoute)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Yalnızca kendi takip ettiğiniz etkinliklerin fiyat analizine erişebilirsiniz."
            });
        }

        return null;
    }
}
