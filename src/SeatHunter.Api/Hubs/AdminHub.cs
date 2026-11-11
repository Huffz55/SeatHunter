using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SeatHunter.Api.Hubs;

[Authorize(Roles = "Admin")]
public class AdminHub : Hub
{
    // Admin dashboard paneli bu Hub'a bağlanarak gerçek zamanlı sistem olaylarını dinler:
    // - "AdminSeatDiscovered"
    // - "AdminDlqReplayed"
    // - "AdminStatsUpdated"
}
