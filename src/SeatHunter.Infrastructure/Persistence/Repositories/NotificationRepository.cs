using Microsoft.EntityFrameworkCore;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;

namespace SeatHunter.Infrastructure.Persistence.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly SeatHunterDbContext _context;

    public NotificationRepository(SeatHunterDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        await _context.Notifications.AddAsync(notification, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<UserNotification>> GetByUserIdAsync(string userId, int take = 50, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

        if (notification == null)
            return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var unread = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
            return 0;

        foreach (var item in unread)
        {
            item.IsRead = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }

    public async Task<int> DeleteReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var readItems = await _context.Notifications
            .Where(n => n.UserId == userId && n.IsRead)
            .ToListAsync(cancellationToken);

        if (readItems.Count == 0)
            return 0;

        _context.Notifications.RemoveRange(readItems);
        await _context.SaveChangesAsync(cancellationToken);
        return readItems.Count;
    }
}
