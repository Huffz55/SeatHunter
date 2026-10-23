using MediatR;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Application.Features.HuntRequests.Commands.CancelHuntRequest;

public record CancelHuntRequestCommand(Guid RequestId, string UserId) : IRequest<bool>;

public class CancelHuntRequestCommandHandler : IRequestHandler<CancelHuntRequestCommand, bool>
{
    private readonly IHuntRequestRepository _repository;
    private readonly IHuntManagerService _huntManager;
    private readonly IDistributedHuntScheduler _scheduler;

    public CancelHuntRequestCommandHandler(
        IHuntRequestRepository repository, 
        IHuntManagerService huntManager,
        IDistributedHuntScheduler scheduler)
    {
        _repository = repository;
        _huntManager = huntManager;
        _scheduler = scheduler;
    }

    public async Task<bool> Handle(CancelHuntRequestCommand request, CancellationToken cancellationToken)
    {
        var hunt = await _repository.GetByIdAsync(request.RequestId, cancellationToken);
        if (hunt == null || hunt.UserId != request.UserId)
        {
            return false; // Bulunamadı veya başka bir kullanıcının avı (IDOR Koruması)
        }

        _huntManager.CancelHunt(request.RequestId.ToString());
        await _scheduler.RemoveHuntAsync(request.RequestId, cancellationToken);

        if (hunt.Status == RequestStatus.Active)
        {
            hunt.Status = RequestStatus.Cancelled;
            await _repository.UpdateAsync(hunt, cancellationToken);
        }

        return true;
    }
}
