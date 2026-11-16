using System.Diagnostics;
using System.Diagnostics.Metrics;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Constants;

namespace SeatHunter.Infrastructure.Telemetry;

public class ScraperMetrics : IScraperMetrics
{
    private static readonly Meter Meter = new(AppConstants.Metrics.MeterName, "1.0.0");

    private readonly Counter<long> _scrapeCounter = Meter.CreateCounter<long>(
        "seathunter_scrapes_total",
        description: "Toplam bilet kazıma ve uygunluk kontrol deneme sayısı");

    private readonly Histogram<double> _scrapeDurationHistogram = Meter.CreateHistogram<double>(
        "seathunter_scrape_duration_ms",
        unit: "ms",
        description: "Kazıma ve sayfa yükleme sürelerinin milisaniye cinsinden dağılımı");

    private readonly Counter<long> _notificationCounter = Meter.CreateCounter<long>(
        "seathunter_notifications_total",
        description: "Kullanıcılara başarıyla gönderilen bildirimlerin kanal bazında sayısı");

    private readonly Counter<long> _checkoutCounter = Meter.CreateCounter<long>(
        "seathunter_billing_checkouts_total",
        description: "Başlatılan abonelik ödeme oturumu sayısı");

    private readonly Counter<long> _webhookCounter = Meter.CreateCounter<long>(
        "seathunter_billing_webhooks_total",
        description: "İşlenen ödeme webhook olayları");

    private readonly Counter<long> _expiredSubscriptionsCounter = Meter.CreateCounter<long>(
        "seathunter_subscriptions_expired_total",
        description: "Süresi dolduğu için Free plana düşürülen abonelik sayısı");

    public void RecordScrapeAttempt(string provider, string status, double durationMs)
    {
        var tags = new TagList
        {
            { "provider", provider },
            { "status", status }
        };

        _scrapeCounter.Add(1, tags);
        _scrapeDurationHistogram.Record(durationMs, tags);
    }

    public void RecordNotificationDispatched(string channel)
    {
        _notificationCounter.Add(1, new KeyValuePair<string, object?>("channel", channel));
    }

    public void RecordCheckoutStarted(string tier, string cycle)
    {
        _checkoutCounter.Add(1, new TagList
        {
            { "tier", tier },
            { "cycle", cycle }
        });
    }

    public void RecordWebhookProcessed(string eventType, string status)
    {
        _webhookCounter.Add(1, new TagList
        {
            { "event_type", eventType },
            { "status", status }
        });
    }

    public void RecordSubscriptionsExpired(int count)
    {
        if (count > 0)
        {
            _expiredSubscriptionsCounter.Add(count);
        }
    }
}
