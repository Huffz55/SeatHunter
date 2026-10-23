namespace SeatHunter.Application.Interfaces;

public interface IDistributedHuntScheduler
{
    Task ScheduleHuntAsync(Guid huntRequestId, DateTime nextScanTime, CancellationToken cancellationToken = default);
    Task RemoveHuntAsync(Guid huntRequestId, CancellationToken cancellationToken = default);
    Task<List<Guid>> FetchDueHuntsAsync(DateTime now, int maxBatchSize = 50, CancellationToken cancellationToken = default);
    Task<long> GetScheduledCountAsync(CancellationToken cancellationToken = default);
}
