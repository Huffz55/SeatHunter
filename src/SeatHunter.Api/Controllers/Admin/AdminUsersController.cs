using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeatHunter.Application.Features.Admin.Commands.ToggleUserStatus;
using SeatHunter.Application.Features.Admin.Commands.UpdateUserTier;
using SeatHunter.Application.Features.Admin.Queries.GetAdminUsers;
using SeatHunter.Domain.Constants;

namespace SeatHunter.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[EnableRateLimiting(AppConstants.RateLimiting.FixedPolicy)]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminUsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _mediator.Send(new GetAdminUsersQuery(), cancellationToken);
        return Ok(users);
    }

    [HttpPut("{id}/tier")]
    public async Task<IActionResult> UpdateTier(string id, [FromBody] UpdateUserTierRequest request, CancellationToken cancellationToken)
    {
        var success = await _mediator.Send(new UpdateUserTierCommand(id, request.Tier), cancellationToken);
        if (!success) return BadRequest(new { message = "Geçersiz plan veya kullanıcı bulunamadı." });
        return Ok(new { message = $"Kullanıcı planı başarıyla {request.Tier} olarak güncellendi." });
    }

    [HttpPost("{id}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(string id, CancellationToken cancellationToken)
    {
        var status = await _mediator.Send(new ToggleUserStatusCommand(id), cancellationToken);
        return Ok(new { message = $"Kullanıcı durumu güncellendi: {(status ? "Aktif" : "Askıya Alındı")}", isActive = status });
    }
}

public class UpdateUserTierRequest
{
    public string Tier { get; set; } = "Free";
}
