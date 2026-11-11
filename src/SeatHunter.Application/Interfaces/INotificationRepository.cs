using SeatHunter.Domain.Entities;

namespace SeatHunter.Application.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(UserNotification notification, CancellationToken cancellationToken = default);
    Task<List<UserNotification>> GetByUserIdAsync(string userId, int take = 50, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> MarkAsReadAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);
    Task<int> DeleteReadAsync(string userId, CancellationToken cancellationToken = default);
}
