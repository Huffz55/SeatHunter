using Microsoft.AspNetCore.SignalR;

namespace SeatHunter.Api.Hubs;

public class NotificationHub : Hub
{
    // Frontend (React/Angular/Vue vb.) bu Hub'a bağlanarak "SeatFound" event'ini dinleyecek.
}
