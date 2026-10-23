namespace SeatHunter.Application.Interfaces;

public interface IScraperMetrics
{
    void RecordScrapeAttempt(string provider, string status, double durationMs);
    void RecordNotificationDispatched(string channel);
}
