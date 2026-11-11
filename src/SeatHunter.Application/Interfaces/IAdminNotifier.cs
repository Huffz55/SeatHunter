namespace SeatHunter.Application.Interfaces;

public interface IAdminNotifier
{
    Task BroadcastSeatDiscoveredAsync(TicketFoundNotification notification, CancellationToken cancellationToken = default);
    Task BroadcastDeadLetterReplayedAsync(int replayedCount, CancellationToken cancellationToken = default);
    Task BroadcastStatsUpdatedAsync(object stats, CancellationToken cancellationToken = default);
}
