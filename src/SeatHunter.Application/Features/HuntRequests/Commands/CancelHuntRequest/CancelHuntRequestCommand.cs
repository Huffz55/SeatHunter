using MediatR;
namespace SeatHunter.Application.Features.HuntRequests.Commands.CancelHuntRequest;

public record CancelHuntRequestCommand(Guid RequestId) : IRequest<bool>;

public class CancelHuntRequestCommandHandler : IRequestHandler<CancelHuntRequestCommand, bool>
{
    public Task<bool> Handle(CancelHuntRequestCommand request, CancellationToken cancellationToken) 
        => Task.FromResult(true);
}
