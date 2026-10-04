using Microsoft.EntityFrameworkCore;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;

namespace SeatHunter.Infrastructure.Persistence.Repositories;

public class HuntRequestRepository : IHuntRequestRepository
{
    private readonly SeatHunterDbContext _context;

    public HuntRequestRepository(SeatHunterDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(HuntRequest huntRequest, CancellationToken cancellationToken)
    {
        await _context.HuntRequests.AddAsync(huntRequest, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<HuntRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.HuntRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(HuntRequest huntRequest, CancellationToken cancellationToken)
    {
        _context.HuntRequests.Update(huntRequest);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
