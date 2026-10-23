using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SeatHunter.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    // Frontend (React/Angular/Vue vb.) bu Hub'a bağlanarak "SeatFound" event'ini dinleyecek.
}
