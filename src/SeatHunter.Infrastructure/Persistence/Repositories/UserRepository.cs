using Microsoft.EntityFrameworkCore;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;

namespace SeatHunter.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly SeatHunterDbContext _context;

    public UserRepository(SeatHunterDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(User user, CancellationToken cancellationToken = default)
    {
        var userHunts = _context.HuntRequests.Where(h => h.UserId == user.Id);
        _context.HuntRequests.RemoveRange(userHunts);
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
