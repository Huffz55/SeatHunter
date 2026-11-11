using FluentAssertions;
using Moq;
using SeatHunter.Application.Features.PriceAnalytics.Queries.GetPriceAnalytics;
using SeatHunter.Application.Features.PriceAnalytics.Queries.GetPriceHistory;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Models;
using Xunit;

namespace SeatHunter.Application.Tests.Features.PriceAnalytics;

public class PriceAnalyticsQueryTests
{
    private readonly Mock<ITicketPriceHistoryService> _mockService = new();

    [Fact]
    public async Task GetPriceAnalyticsQueryHandler_ShouldReturnSummaryFromService()
    {
        // Arrange
        var summary = new PriceAnalyticsSummary(
            RouteOrEventId: "https://biletix.com/derbi",
            CurrentPrice: 1500m,
            MinPrice: 1200m,
            MaxPrice: 2000m,
            AveragePrice: 1600m,
            PriceDropPercentage: 25.0m,
            TotalSnapshotsCount: 10,
            FirstRecordedAt: DateTime.UtcNow.AddDays(-14),
            LastRecordedAt: DateTime.UtcNow
        );

        _mockService.Setup(s => s.GetPriceAnalyticsAsync("https://biletix.com/derbi", It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var handler = new GetPriceAnalyticsQueryHandler(_mockService.Object);
        var query = new GetPriceAnalyticsQuery("https://biletix.com/derbi");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentPrice.Should().Be(1500m);
        result.MinPrice.Should().Be(1200m);
        result.MaxPrice.Should().Be(2000m);
        result.PriceDropPercentage.Should().Be(25.0m);
    }

    [Fact]
    public async Task GetPriceHistoryQueryHandler_ShouldReturnMappedDtos()
    {
        // Arrange
        var snapshots = new List<PriceSnapshot>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RouteOrEventId = "route-1",
                ProviderName = "Obilet",
                EventName = "Ankara - Izmir",
                CategoryName = "2+1 Otobüs",
                Price = 650m,
                Currency = "TRY",
                RecordedAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                RouteOrEventId = "route-1",
                ProviderName = "Obilet",
                EventName = "Ankara - Izmir",
                CategoryName = "2+1 Otobüs",
                Price = 580m,
                Currency = "TRY",
                RecordedAt = DateTime.UtcNow
            }
        };

        _mockService.Setup(s => s.GetPriceHistoryAsync("route-1", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        var handler = new GetPriceHistoryQueryHandler(_mockService.Object);
        var query = new GetPriceHistoryQuery("route-1", Days: 7);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Price.Should().Be(650m);
        result[1].Price.Should().Be(580m);
        result[1].CategoryName.Should().Be("2+1 Otobüs");
    }
}
