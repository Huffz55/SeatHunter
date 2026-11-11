using MediatR;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Application.Features.Admin.Queries.GetDeadLetterStatus;

public record GetDeadLetterStatusQuery : IRequest<DeadLetterQueueStatus>;

public class GetDeadLetterStatusQueryHandler : IRequestHandler<GetDeadLetterStatusQuery, DeadLetterQueueStatus>
{
    private readonly IDeadLetterReplayService _replayService;

    public GetDeadLetterStatusQueryHandler(IDeadLetterReplayService replayService)
    {
        _replayService = replayService;
    }

    public async Task<DeadLetterQueueStatus> Handle(GetDeadLetterStatusQuery request, CancellationToken cancellationToken)
    {
        return await _replayService.GetStatusAsync(cancellationToken);
    }
}
