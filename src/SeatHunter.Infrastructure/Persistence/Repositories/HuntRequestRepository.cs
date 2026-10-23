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

    public async Task<List<HuntRequest>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _context.HuntRequests
            .Where(x => x.UserId == userId && x.Status == Domain.Enums.RequestStatus.Active)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetActiveCountByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.HuntRequests
            .CountAsync(x => x.UserId == userId && x.Status == Domain.Enums.RequestStatus.Active, cancellationToken);
    }

    public async Task<List<HuntRequest>> GetRecentByUserIdAsync(string userId, int count, CancellationToken cancellationToken)
    {
        return await _context.HuntRequests
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<HuntRequest>> GetHistoryByUserIdAsync(string userId, int count, CancellationToken cancellationToken)
    {
        return await _context.HuntRequests
            .Where(x => x.UserId == userId && (x.Status == Domain.Enums.RequestStatus.Completed || x.Status == Domain.Enums.RequestStatus.Cancelled || x.Status == Domain.Enums.RequestStatus.Expired))
            .OrderByDescending(x => x.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
