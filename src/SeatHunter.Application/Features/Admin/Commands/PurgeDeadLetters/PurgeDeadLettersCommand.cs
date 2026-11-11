using MediatR;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Application.Features.Admin.Commands.PurgeDeadLetters;

public record PurgeDeadLettersCommand : IRequest<uint>;

public class PurgeDeadLettersCommandHandler : IRequestHandler<PurgeDeadLettersCommand, uint>
{
    private readonly IDeadLetterReplayService _replayService;

    public PurgeDeadLettersCommandHandler(IDeadLetterReplayService replayService)
    {
        _replayService = replayService;
    }

    public async Task<uint> Handle(PurgeDeadLettersCommand request, CancellationToken cancellationToken)
    {
        return await _replayService.PurgeQueueAsync(cancellationToken);
    }
}
