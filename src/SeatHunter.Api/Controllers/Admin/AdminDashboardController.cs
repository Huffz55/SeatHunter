using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeatHunter.Application.Features.Admin.Queries.GetSystemDashboardOverview;
using SeatHunter.Domain.Constants;

namespace SeatHunter.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[EnableRateLimiting(AppConstants.RateLimiting.FixedPolicy)]
[Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminDashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var overview = await _mediator.Send(new GetSystemDashboardOverviewQuery(), cancellationToken);
        return Ok(overview);
    }
}
