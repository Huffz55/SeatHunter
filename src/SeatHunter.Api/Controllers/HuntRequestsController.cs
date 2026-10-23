using MediatR;
using Microsoft.AspNetCore.Mvc;
using SeatHunter.Application.Features.HuntRequests.Commands.CreateHuntRequest;
using SeatHunter.Application.Features.HuntRequests.Commands.CancelHuntRequest;
using SeatHunter.Application.Features.HuntRequests.Queries;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

using SeatHunter.Domain.Constants;

namespace SeatHunter.Api.Controllers;

[ApiController]
[Authorize]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(AppConstants.RateLimiting.FixedPolicy)]
[Route("api/[controller]")]
public class HuntRequestsController : ControllerBase
{
    private readonly IMediator _mediator;
    public HuntRequestsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHuntRequestCommand command)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var finalCommand = command with { UserId = userId };
        return Ok(new { HuntRequestId = await _mediator.Send(finalCommand) });
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        return Ok(await _mediator.Send(new GetActiveRequestsQuery(userId)));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var success = await _mediator.Send(new CancelHuntRequestCommand(id, userId));
        if (!success) return NotFound(new { Message = "Tarama isteği bulunamadı veya bu işlem için yetkiniz yok." });

        return Ok(new { Message = "Tarama başarıyla iptal edildi." });
    }
}
