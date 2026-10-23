using FluentAssertions;
using SeatHunter.Infrastructure.Telemetry;
using Xunit;

namespace SeatHunter.Infrastructure.Tests;

public class ScraperMetricsTests
{
    [Fact]
    public void RecordScrapeAttempt_ShouldRecordWithoutError()
    {
        // Arrange
        var metrics = new ScraperMetrics();

        // Act & Assert
        var act = () => metrics.RecordScrapeAttempt("BiletixLive", "found", 154.2);
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordNotificationDispatched_ShouldRecordWithoutError()
    {
        // Arrange
        var metrics = new ScraperMetrics();

        // Act & Assert
        var act = () =>
        {
            metrics.RecordNotificationDispatched("Email");
            metrics.RecordNotificationDispatched("Telegram");
            metrics.RecordNotificationDispatched("Discord");
            metrics.RecordNotificationDispatched("SignalR");
        };

        act.Should().NotThrow();
    }
}
