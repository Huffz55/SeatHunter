using MediatR;

namespace SeatHunter.Application.Features.HuntRequests.Commands.CreateHuntRequest;

public record CreateHuntRequestCommand(
    string UserId, 
    string Url, 
    DateTime TargetDate,
    string Language,
    string? TargetEventName) : IRequest<Guid>;
