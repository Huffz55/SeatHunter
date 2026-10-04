using Microsoft.EntityFrameworkCore;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;

namespace SeatHunter.Infrastructure.Persistence.Repositories;

public class RouteRepository : IRouteRepository
{
    private readonly SeatHunterDbContext _context;

    public RouteRepository(SeatHunterDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Route route, CancellationToken cancellationToken)
    {
        await _context.Routes.AddAsync(route, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Route?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return await _context.Routes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Route route, CancellationToken cancellationToken)
    {
        _context.Routes.Update(route);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
