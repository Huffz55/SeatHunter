using MediatR;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Application.Features.Admin.Commands.ReplayDeadLetters;

public record ReplayDeadLettersCommand(int MaxCount = 100) : IRequest<int>;

public class ReplayDeadLettersCommandHandler : IRequestHandler<ReplayDeadLettersCommand, int>
{
    private readonly IDeadLetterReplayService _replayService;

    public ReplayDeadLettersCommandHandler(IDeadLetterReplayService replayService)
    {
        _replayService = replayService;
    }

    public async Task<int> Handle(ReplayDeadLettersCommand request, CancellationToken cancellationToken)
    {
        return await _replayService.ReplayMessagesAsync(request.MaxCount, cancellationToken);
    }
}
