using SeatHunter.Domain.Entities;

namespace SeatHunter.Application.Interfaces;

public interface ITicketRepository
{
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken);
    Task<Ticket?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken);
}
