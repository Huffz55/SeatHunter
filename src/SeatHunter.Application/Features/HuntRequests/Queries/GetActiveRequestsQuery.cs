using MediatR;
using SeatHunter.Domain.Entities;
namespace SeatHunter.Application.Features.HuntRequests.Queries;

public record GetActiveRequestsQuery(string UserId) : IRequest<List<HuntRequest>>;

public class GetActiveRequestsQueryHandler : IRequestHandler<GetActiveRequestsQuery, List<HuntRequest>>
{
    public Task<List<HuntRequest>> Handle(GetActiveRequestsQuery request, CancellationToken cancellationToken) 
        => Task.FromResult(new List<HuntRequest>());
}
