using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Models;
using SeatHunter.Infrastructure.Services;
using Xunit;

namespace SeatHunter.Infrastructure.Tests;

public class TicketPriceHistoryServiceTests
{
    private readonly Mock<IPriceHistoryRepository> _mockRepo = new();
    private readonly Mock<ILogger<TicketPriceHistoryService>> _mockLogger = new();
    private readonly TicketPriceHistoryService _service;

    public TicketPriceHistoryServiceTests()
    {
        _service = new TicketPriceHistoryService(_mockRepo.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task RecordPriceObservationAsync_WhenNoPreviousSnapshot_ShouldNotTriggerPriceDrop()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetLatestSnapshotAsync("event-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceSnapshot?)null);

        // Act
        var result = await _service.RecordPriceObservationAsync(
            routeOrEventId: "event-1",
            providerName: "Biletix",
            eventName: "Rock Festival",
            observedPrice: 1000m
        );

        // Assert
        result.IsPriceDrop.Should().BeFalse();
        result.DropPercentage.Should().BeNull();
        result.Snapshot.Price.Should().Be(1000m);
        _mockRepo.Verify(r => r.AddSnapshotAsync(It.Is<PriceSnapshot>(p => p.Price == 1000m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordPriceObservationAsync_WhenPriceDrops_ShouldDetectDropAndCalculatePercentage()
    {
        // Arrange: Önceki fiyat 1000 TL, yeni fiyat 800 TL (%20 düşüş)
        var previous = new PriceSnapshot
        {
            RouteOrEventId = "event-1",
            Price = 1000m,
            RecordedAt = DateTime.UtcNow.AddHours(-2)
        };

        _mockRepo.Setup(r => r.GetLatestSnapshotAsync("event-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(previous);

        // Act
        var result = await _service.RecordPriceObservationAsync(
            routeOrEventId: "event-1",
            providerName: "Biletix",
            eventName: "Rock Festival",
            observedPrice: 800m
        );

        // Assert
        result.IsPriceDrop.Should().BeTrue();
        result.PreviousPrice.Should().Be(1000m);
        result.DropPercentage.Should().Be(20.0m);
    }

    [Fact]
    public async Task RecordPriceObservationAsync_WhenPriceIncreases_ShouldNotTriggerPriceDrop()
    {
        // Arrange: Önceki fiyat 800 TL, yeni fiyat 950 TL (Fiyat arttı)
        var previous = new PriceSnapshot
        {
            RouteOrEventId = "event-1",
            Price = 800m,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        };

        _mockRepo.Setup(r => r.GetLatestSnapshotAsync("event-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(previous);

        // Act
        var result = await _service.RecordPriceObservationAsync(
            routeOrEventId: "event-1",
            providerName: "Biletix",
            eventName: "Rock Festival",
            observedPrice: 950m
        );

        // Assert
        result.IsPriceDrop.Should().BeFalse();
        result.DropPercentage.Should().BeNull();
    }

    [Fact]
    public async Task RecordPriceObservationAsync_WhenPriceDropsBelowUserMaxPrice_ShouldTriggerAlert()
    {
        // Arrange: Kullanıcı bütçesi 500 TL. Önceki fiyat 600 TL, yeni fiyat 450 TL
        var previous = new PriceSnapshot
        {
            RouteOrEventId = "event-1",
            Price = 600m,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        };

        _mockRepo.Setup(r => r.GetLatestSnapshotAsync("event-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(previous);

        // Act
        var result = await _service.RecordPriceObservationAsync(
            routeOrEventId: "event-1",
            providerName: "Biletix",
            eventName: "Tiyatro",
            observedPrice: 450m,
            userMaxPrice: 500m
        );

        // Assert
        result.IsPriceDrop.Should().BeTrue();
        result.DropPercentage.Should().Be(25.0m); // ((600-450)/600)*100 = 25%
    }

    [Fact]
    public async Task GetPriceAnalyticsAsync_ShouldReturnAnalyticsSummary()
    {
        // Arrange
        var summary = new PriceAnalyticsSummary(
            RouteOrEventId: "event-1",
            CurrentPrice: 750m,
            MinPrice: 600m,
            MaxPrice: 1200m,
            AveragePrice: 850m,
            PriceDropPercentage: 37.5m,
            TotalSnapshotsCount: 5,
            FirstRecordedAt: DateTime.UtcNow.AddDays(-7),
            LastRecordedAt: DateTime.UtcNow
        );

        _mockRepo.Setup(r => r.GetAnalyticsSummaryAsync("event-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        // Act
        var result = await _service.GetPriceAnalyticsAsync("event-1");

        // Assert
        result.Should().NotBeNull();
        result!.MinPrice.Should().Be(600m);
        result.MaxPrice.Should().Be(1200m);
        result.CurrentPrice.Should().Be(750m);
        result.AveragePrice.Should().Be(850m);
        result.PriceDropPercentage.Should().Be(37.5m);
    }
}
