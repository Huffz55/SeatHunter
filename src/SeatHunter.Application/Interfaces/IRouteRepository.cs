using SeatHunter.Domain.Entities;

namespace SeatHunter.Application.Interfaces;

public interface IRouteRepository
{
    Task AddAsync(Route route, CancellationToken cancellationToken);
    Task<Route?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task UpdateAsync(Route route, CancellationToken cancellationToken);
}
