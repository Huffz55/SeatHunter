using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatHunter.Application.DTOs;
using SeatHunter.Application.Features.HuntRequests.Commands.CancelHuntRequest;
using SeatHunter.Application.Features.HuntRequests.Queries;
using SeatHunter.Application.Features.HuntRequests.Queries.GetHuntHistory;
using SeatHunter.Domain.Constants;
using System.Security.Claims;

namespace SeatHunter.Api.Controllers;

[ApiController]
[Authorize]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(AppConstants.RateLimiting.FixedPolicy)]
[Route("api/[controller]")]
public class HuntsController : ControllerBase
{
    private readonly IMediator _mediator;

    public HuntsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("cancel/{id}")]
    public async Task<IActionResult> Cancel(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (!Guid.TryParse(id, out var guid))
        {
            return BadRequest(new { Message = "Geçersiz Hunt ID formatı." });
        }

        var success = await _mediator.Send(new CancelHuntRequestCommand(guid, userId));
        if (!success)
        {
            return NotFound(new { Message = "Tarama isteği bulunamadı veya bu işlem için yetkiniz yok." });
        }

        return Ok(new { Message = "Tarama başarıyla iptal edildi.", HuntId = id });
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var activeRequests = await _mediator.Send(new GetActiveRequestsQuery(userId));
        var result = activeRequests.Select(r => new ActiveHuntDto
        {
            Id = r.Id.ToString(),
            Provider = r.ProviderName,
            EventId = r.RouteOrEventId,
            EventName = r.EventName ?? string.Empty,
            Status = "Active",
            StartTime = r.CreatedAt.ToLocalTime().ToString("HH:mm"),
            MaxPrice = r.MaxPrice,
            PreferredCategory = r.PreferredCategory,
            AutoReserveEnabled = r.AutoReserveEnabled
        }).ToList();

        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _mediator.Send(new GetHuntHistoryQuery(userId));
        return Ok(result);
    }
}
