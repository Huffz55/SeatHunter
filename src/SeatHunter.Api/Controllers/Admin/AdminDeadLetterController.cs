using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeatHunter.Application.Features.Admin.Commands.PurgeDeadLetters;
using SeatHunter.Application.Features.Admin.Commands.ReplayDeadLetters;
using SeatHunter.Application.Features.Admin.Queries.GetDeadLetterStatus;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Constants;

namespace SeatHunter.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[EnableRateLimiting(AppConstants.RateLimiting.FixedPolicy)]
[Route("api/admin/dlq")]
public class AdminDeadLetterController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAdminNotifier _adminNotifier;

    public AdminDeadLetterController(IMediator mediator, IAdminNotifier adminNotifier)
    {
        _mediator = mediator;
        _adminNotifier = adminNotifier;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _mediator.Send(new GetDeadLetterStatusQuery(), cancellationToken);
        return Ok(status);
    }

    [HttpPost("replay")]
    public async Task<IActionResult> Replay([FromQuery] int maxCount = 100, CancellationToken cancellationToken = default)
    {
        var replayedCount = await _mediator.Send(new ReplayDeadLettersCommand(maxCount), cancellationToken);
        await _adminNotifier.BroadcastDeadLetterReplayedAsync(replayedCount, cancellationToken);
        return Ok(new { message = $"{replayedCount} poison mesaj ana kuyruğa yeniden yönlendirildi.", replayedCount });
    }

    [HttpDelete("purge")]
    public async Task<IActionResult> Purge(CancellationToken cancellationToken)
    {
        var purgedCount = await _mediator.Send(new PurgeDeadLettersCommand(), cancellationToken);
        return Ok(new { message = $"{purgedCount} mesaj DLQ kuyruğundan kalıcı olarak temizlendi.", purgedCount });
    }
}
