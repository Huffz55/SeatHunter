namespace SeatHunter.Application.Interfaces;

public record DeadLetterQueueStatus(
    string QueueName,
    uint MessageCount,
    uint ConsumerCount,
    DateTime CheckedAt
);

public interface IDeadLetterReplayService
{
    Task<DeadLetterQueueStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<int> ReplayMessagesAsync(int maxCount = 100, CancellationToken cancellationToken = default);
    Task<uint> PurgeQueueAsync(CancellationToken cancellationToken = default);
}
