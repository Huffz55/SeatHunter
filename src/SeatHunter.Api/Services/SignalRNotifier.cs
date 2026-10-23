using Microsoft.AspNetCore.SignalR;
using SeatHunter.Api.Hubs;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Constants;

namespace SeatHunter.Api.Services;

public class SignalRNotifier : ISignalRNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotifier(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendSeatFoundAsync(string userId, Guid requestId, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        await _hubContext.Clients.User(userId).SendAsync(AppConstants.SignalREvents.SeatFound, new 
        { 
            requestId = requestId, 
            message = message 
        }, cancellationToken);
    }
}
