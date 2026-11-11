using Microsoft.AspNetCore.SignalR;
using SeatHunter.Api.Hubs;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Api.Services;

public class AdminNotifier : IAdminNotifier
{
    private readonly IHubContext<AdminHub> _hubContext;
    private readonly ILogger<AdminNotifier> _logger;

    public AdminNotifier(IHubContext<AdminHub> hubContext, ILogger<AdminNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastSeatDiscoveredAsync(TicketFoundNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("AdminSeatDiscovered", notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin canlı yayınında koltuk bildirimi iletilirken hata oluştu.");
        }
    }

    public async Task BroadcastDeadLetterReplayedAsync(int replayedCount, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("AdminDlqReplayed", new { replayedCount, timestamp = DateTime.UtcNow }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin canlı yayınında DLQ replay bildirimi iletilirken hata oluştu.");
        }
    }

    public async Task BroadcastStatsUpdatedAsync(object stats, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("AdminStatsUpdated", stats, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin canlı yayınında istatistik güncellemesi iletilirken hata oluştu.");
        }
    }
}
