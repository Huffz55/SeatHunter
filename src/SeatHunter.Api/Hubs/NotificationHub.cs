using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SeatHunter.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    // Frontend bu Hub'a bağlanarak "SeatFound" event'ini dinler.
}
