using MediatR;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Application.Features.HuntRequests.Commands.CancelHuntRequest;

public record CancelHuntRequestCommand(Guid RequestId, string UserId) : IRequest<bool>;

public class CancelHuntRequestCommandHandler : IRequestHandler<CancelHuntRequestCommand, bool>
{
    private readonly IHuntRequestRepository _repository;
    private readonly IHuntManagerService _huntManager;
    private readonly IDistributedHuntScheduler _scheduler;
    private readonly INotificationRepository? _notificationRepository;

    public CancelHuntRequestCommandHandler(
        IHuntRequestRepository repository, 
        IHuntManagerService huntManager,
        IDistributedHuntScheduler scheduler,
        INotificationRepository? notificationRepository = null)
    {
        _repository = repository;
        _huntManager = huntManager;
        _scheduler = scheduler;
        _notificationRepository = notificationRepository;
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

        if (hunt.Status != RequestStatus.Cancelled)
        {
            hunt.Status = RequestStatus.Cancelled;
            await _repository.UpdateAsync(hunt, cancellationToken);

            if (_notificationRepository != null)
            {
                var label = !string.IsNullOrWhiteSpace(hunt.EventName) ? hunt.EventName : hunt.RouteOrEventId;
                await _notificationRepository.AddAsync(new UserNotification
                {
                    UserId = request.UserId,
                    Title = "Tarama İptal Edildi",
                    Message = $"\"{label}\" için aktif bilet takibi iptal edildi.",
                    Type = "warning",
                    RelatedUrl = hunt.RouteOrEventId
                }, cancellationToken);
            }
        }

        return true;
    }
}
