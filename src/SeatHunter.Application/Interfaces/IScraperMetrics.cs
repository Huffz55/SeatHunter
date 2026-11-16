namespace SeatHunter.Application.Interfaces;

public interface IScraperMetrics
{
    void RecordScrapeAttempt(string provider, string status, double durationMs);
    void RecordNotificationDispatched(string channel);
    void RecordCheckoutStarted(string tier, string cycle) { }
    void RecordWebhookProcessed(string eventType, string status) { }
    void RecordSubscriptionsExpired(int count) { }
}
