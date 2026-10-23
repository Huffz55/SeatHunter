using MediatR;

namespace SeatHunter.Application.Features.HuntRequests.Commands.CreateHuntRequest;

public record CreateHuntRequestCommand(
    string UserId, 
    string Url, 
    DateTime TargetDate,
    string Language = "tr",
    string? TargetEventName = null,
    decimal? MaxPrice = null,
    string? PreferredCategory = null,
    bool AutoReserveEnabled = false) : IRequest<Guid>;
