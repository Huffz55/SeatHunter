using Microsoft.EntityFrameworkCore;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;

namespace SeatHunter.Infrastructure.Persistence.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly SeatHunterDbContext _context;

    public TicketRepository(SeatHunterDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        await _context.Tickets.AddAsync(ticket, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Ticket?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return await _context.Tickets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
