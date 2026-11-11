using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Constants;
using System.Security.Claims;

namespace SeatHunter.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting(AppConstants.RateLimiting.FixedPolicy)]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _notificationRepository;

    public NotificationsController(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var clampedTake = Math.Clamp(take, 1, 100);
        var items = await _notificationRepository.GetByUserIdAsync(userId, clampedTake, cancellationToken);
        var unreadCount = await _notificationRepository.GetUnreadCountAsync(userId, cancellationToken);

        return Ok(new
        {
            items = items.Select(n => new
            {
                id = n.Id,
                title = n.Title,
                message = n.Message,
                type = n.Type,
                relatedUrl = n.RelatedUrl,
                isRead = n.IsRead,
                createdAt = n.CreatedAt
            }),
            unreadCount
        });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var found = await _notificationRepository.MarkAsReadAsync(id, userId, cancellationToken);
        if (!found) return NotFound(new { message = "Bildirim bulunamadı." });

        var unreadCount = await _notificationRepository.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new { unreadCount });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var markedCount = await _notificationRepository.MarkAllAsReadAsync(userId, cancellationToken);
        return Ok(new { unreadCount = 0, markedCount });
    }

    [HttpDelete("read")]
    public async Task<IActionResult> DeleteReadNotifications(CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var deletedCount = await _notificationRepository.DeleteReadAsync(userId, cancellationToken);
        return Ok(new { deletedCount });
    }
}
