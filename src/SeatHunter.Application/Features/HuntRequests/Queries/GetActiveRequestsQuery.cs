using MediatR;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;

namespace SeatHunter.Application.Features.HuntRequests.Queries;

public record GetActiveRequestsQuery(string UserId) : IRequest<List<HuntRequest>>;

public class GetActiveRequestsQueryHandler : IRequestHandler<GetActiveRequestsQuery, List<HuntRequest>>
{
    private readonly IHuntRequestRepository _repository;

    public GetActiveRequestsQueryHandler(IHuntRequestRepository repository)
    {
        _repository = repository;
    }

    public Task<List<HuntRequest>> Handle(GetActiveRequestsQuery request, CancellationToken cancellationToken)
    {
        return _repository.GetActiveByUserIdAsync(request.UserId, cancellationToken);
    }
}
