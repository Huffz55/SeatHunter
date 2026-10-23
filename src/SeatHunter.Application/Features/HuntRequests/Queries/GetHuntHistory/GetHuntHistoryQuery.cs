using MediatR;
using SeatHunter.Application.DTOs;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Application.Features.HuntRequests.Queries.GetHuntHistory;

public record GetHuntHistoryQuery(string UserId, int Take = 100) : IRequest<List<HuntHistoryDto>>;

public class GetHuntHistoryQueryHandler : IRequestHandler<GetHuntHistoryQuery, List<HuntHistoryDto>>
{
    private readonly IHuntRequestRepository _repository;

    public GetHuntHistoryQueryHandler(IHuntRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<HuntHistoryDto>> Handle(GetHuntHistoryQuery request, CancellationToken cancellationToken)
    {
        var history = await _repository.GetHistoryByUserIdAsync(request.UserId, request.Take, cancellationToken);
        return history.Select(r => new HuntHistoryDto
        {
            Id = r.Id.ToString(),
            Provider = r.ProviderName,
            EventId = r.RouteOrEventId,
            EventName = r.EventName ?? string.Empty,
            Status = r.Status switch
            {
                RequestStatus.Completed => "Found",
                RequestStatus.Cancelled => "Cancelled",
                RequestStatus.Expired => "Expired",
                _ => r.Status.ToString()
            },
            Date = r.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
        }).ToList();
    }
}
