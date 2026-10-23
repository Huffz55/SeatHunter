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
}
