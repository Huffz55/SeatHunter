using SeatHunter.Domain.Entities;

namespace SeatHunter.Application.Interfaces;

public interface IHuntRequestRepository
{
    Task AddAsync(HuntRequest huntRequest, CancellationToken cancellationToken);
    Task<HuntRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateAsync(HuntRequest huntRequest, CancellationToken cancellationToken);
    Task<List<HuntRequest>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task<int> GetActiveCountByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<List<HuntRequest>> GetRecentByUserIdAsync(string userId, int count, CancellationToken cancellationToken);
    Task<List<HuntRequest>> GetHistoryByUserIdAsync(string userId, int count, CancellationToken cancellationToken);
}
