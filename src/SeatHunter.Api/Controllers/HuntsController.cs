using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class HuntsController : ControllerBase
{
    private readonly IHuntManagerService _huntManager;

    public HuntsController(IHuntManagerService huntManager)
    {
        _huntManager = huntManager;
    }

    [HttpPost("cancel/{id}")]
    public async Task<IActionResult> Cancel(string id, [FromServices] SeatHunter.Infrastructure.Persistence.SeatHunterDbContext dbContext)
    {
        _huntManager.CancelHunt(id);
        
        if (Guid.TryParse(id, out var guid))
        {
            var hunt = await dbContext.HuntRequests.FindAsync(guid);
            if (hunt != null && hunt.Status == SeatHunter.Domain.Enums.RequestStatus.Active)
            {
                hunt.Status = SeatHunter.Domain.Enums.RequestStatus.Cancelled;
                await dbContext.SaveChangesAsync();
            }
        }
        
        return Ok(new { Message = "Tarama başarıyla iptal edildi.", HuntId = id });
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive([FromServices] SeatHunter.Infrastructure.Persistence.SeatHunterDbContext dbContext)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        var allHunts = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            System.Linq.Queryable.OrderByDescending(System.Linq.Queryable.Where(dbContext.HuntRequests, r => r.UserId == userId), r => r.CreatedAt).Take(100)
        );

        var result = System.Linq.Enumerable.Select(allHunts, r => new SeatHunter.Application.DTOs.ActiveHuntDto
        {
            Id = r.Id.ToString(),
            Provider = r.ProviderName,
            EventId = r.RouteOrEventId,
            EventName = r.EventName,
            Status = r.Status.ToString() == "Completed" ? "Found" : r.Status.ToString(),
            StartTime = r.CreatedAt.ToLocalTime().ToString("HH:mm")
        });

        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromServices] SeatHunter.Infrastructure.Persistence.SeatHunterDbContext dbContext)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        // 1 = Completed (Found), 2 = Cancelled
        var history = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            System.Linq.Queryable.OrderByDescending(
                System.Linq.Queryable.Where(dbContext.HuntRequests, r => r.UserId == userId && (r.Status == SeatHunter.Domain.Enums.RequestStatus.Completed || r.Status == SeatHunter.Domain.Enums.RequestStatus.Cancelled)), 
                r => r.CreatedAt
            ).Take(100)
        );

        var result = System.Linq.Enumerable.Select(history, r => new
        {
            Id = r.Id.ToString(),
            Provider = r.ProviderName,
            EventId = r.RouteOrEventId,
            EventName = r.EventName,
            Status = r.Status == SeatHunter.Domain.Enums.RequestStatus.Completed ? "Found" : "Cancelled",
            Date = r.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
        });

        return Ok(result);
    }
}
