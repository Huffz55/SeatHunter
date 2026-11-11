using MediatR;

namespace SeatHunter.Application.Features.HuntRequests.Commands.CreateHuntRequest;

public record CreateHuntRequestCommand(
    string UserId = "", 
    string Url = "", 
    DateTime TargetDate = default,
    string Language = "tr",
    string? TargetEventName = null,
    decimal? MaxPrice = null,
    string? PreferredCategory = null,
    bool AutoReserveEnabled = false) : IRequest<Guid>
{
    public CreateHuntRequestCommand() : this("", "", default, "tr", null, null, null, false) { }
}
