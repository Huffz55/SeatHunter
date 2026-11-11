using MediatR;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Application.Features.Admin.Commands.ToggleUserStatus;

public record ToggleUserStatusCommand(string UserId) : IRequest<bool>;

public class ToggleUserStatusCommandHandler : IRequestHandler<ToggleUserStatusCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public ToggleUserStatusCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> Handle(ToggleUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null) return false;

        user.IsActive = !user.IsActive;
        await _userRepository.UpdateAsync(user, cancellationToken);
        return user.IsActive;
    }
}
