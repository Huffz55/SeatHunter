using MediatR;
using SeatHunter.Application.DTOs;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Application.Features.Admin.Queries.GetAdminUsers;

public record GetAdminUsersQuery : IRequest<List<AdminUserDto>>;

public class GetAdminUsersQueryHandler : IRequestHandler<GetAdminUsersQuery, List<AdminUserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IHuntRequestRepository _huntRequestRepository;

    public GetAdminUsersQueryHandler(IUserRepository userRepository, IHuntRequestRepository huntRequestRepository)
    {
        _userRepository = userRepository;
        _huntRequestRepository = huntRequestRepository;
    }

    public async Task<List<AdminUserDto>> Handle(GetAdminUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetAllUsersAsync(cancellationToken);
        var result = new List<AdminUserDto>();

        foreach (var user in users)
        {
            var activeHunts = await _huntRequestRepository.GetActiveCountByUserIdAsync(user.Id, cancellationToken);
            var totalHuntsList = await _huntRequestRepository.GetRecentByUserIdAsync(user.Id, 1000, cancellationToken);
            
            result.Add(new AdminUserDto(
                Id: user.Id,
                Email: user.Email,
                FirstName: user.FirstName,
                LastName: user.LastName,
                Tier: user.Tier.ToString(),
                Role: user.Role,
                IsActive: user.IsActive,
                ActiveHuntCount: activeHunts,
                TotalHuntCount: totalHuntsList.Count,
                CreatedAt: user.CreatedAt
            ));
        }

        return result;
    }
}
