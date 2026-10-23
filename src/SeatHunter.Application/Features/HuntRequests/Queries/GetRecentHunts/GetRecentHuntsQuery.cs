using MediatR;
using SeatHunter.Application.DTOs;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Application.Features.HuntRequests.Queries.GetRecentHunts;

public record GetRecentHuntsQuery(string UserId, int Take = 100) : IRequest<List<ActiveHuntDto>>;

public class GetRecentHuntsQueryHandler : IRequestHandler<GetRecentHuntsQuery, List<ActiveHuntDto>>
{
    private readonly IHuntRequestRepository _repository;

    public GetRecentHuntsQueryHandler(IHuntRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ActiveHuntDto>> Handle(GetRecentHuntsQuery request, CancellationToken cancellationToken)
    {
        var hunts = await _repository.GetRecentByUserIdAsync(request.UserId, request.Take, cancellationToken);
        return hunts.Select(r => new ActiveHuntDto
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
            StartTime = r.CreatedAt.ToLocalTime().ToString("HH:mm")
        }).ToList();
    }
}
