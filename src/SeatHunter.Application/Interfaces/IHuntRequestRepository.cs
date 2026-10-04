using SeatHunter.Domain.Entities;

namespace SeatHunter.Application.Interfaces;

public interface IHuntRequestRepository
{
    Task AddAsync(HuntRequest huntRequest, CancellationToken cancellationToken);
    Task<HuntRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateAsync(HuntRequest huntRequest, CancellationToken cancellationToken);
}
