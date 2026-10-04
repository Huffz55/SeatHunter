using MediatR;
using Microsoft.AspNetCore.Mvc;
using SeatHunter.Application.Features.HuntRequests.Commands.CreateHuntRequest;
using SeatHunter.Application.Features.HuntRequests.Commands.CancelHuntRequest;
using SeatHunter.Application.Features.HuntRequests.Queries;
using Microsoft.AspNetCore.Authorization;

namespace SeatHunter.Api.Controllers;

[ApiController]
[Authorize]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("FixedPolicy")]
[Route("api/[controller]")]
public class HuntRequestsController : ControllerBase
{
    private readonly IMediator _mediator;
    public HuntRequestsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHuntRequestCommand command)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "admin";
        var finalCommand = command with { UserId = userId };
        return Ok(new { HuntRequestId = await _mediator.Send(finalCommand) });
    }

    [HttpGet("active/{userId}")]
    public async Task<IActionResult> GetActive(string userId) => Ok(await _mediator.Send(new GetActiveRequestsQuery(userId)));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancel(Guid id) => Ok(await _mediator.Send(new CancelHuntRequestCommand(id)));
}
