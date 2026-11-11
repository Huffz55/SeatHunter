using MediatR;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Application.Features.Admin.Commands.UpdateUserTier;

public record UpdateUserTierCommand(string UserId, string Tier) : IRequest<bool>;

public class UpdateUserTierCommandHandler : IRequestHandler<UpdateUserTierCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public UpdateUserTierCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> Handle(UpdateUserTierCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null) return false;

        if (Enum.TryParse<UserTier>(request.Tier, true, out var newTier))
        {
            user.Tier = newTier;
            await _userRepository.UpdateAsync(user, cancellationToken);
            return true;
        }

        return false;
    }
}
