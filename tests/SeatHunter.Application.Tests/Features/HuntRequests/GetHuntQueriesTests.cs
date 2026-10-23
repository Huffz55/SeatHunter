using FluentAssertions;
using Moq;
using SeatHunter.Application.Features.HuntRequests.Queries.GetHuntHistory;
using SeatHunter.Application.Features.HuntRequests.Queries.GetRecentHunts;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Enums;
using Xunit;

namespace SeatHunter.Application.Tests.Features.HuntRequests;

public class GetHuntQueriesTests
{
    [Fact]
    public async Task GetHuntHistoryQueryHandler_ShouldReturnMappedDtos()
    {
        // Arrange
        var mockRepo = new Mock<IHuntRequestRepository>();
        var sampleHunts = new List<HuntRequest>
        {
            new HuntRequest
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                ProviderName = "Universal",
                RouteOrEventId = "https://example.com/event-1",
                EventName = "Rock Festival",
                Status = RequestStatus.Completed,
                CreatedAt = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc)
            },
            new HuntRequest
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                ProviderName = "BiletixLive",
                RouteOrEventId = "https://biletix.com/event-2",
                EventName = "Jazz Night",
                Status = RequestStatus.Cancelled,
                CreatedAt = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc)
            }
        };

        mockRepo.Setup(r => r.GetHistoryByUserIdAsync("user-1", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleHunts);

        var handler = new GetHuntHistoryQueryHandler(mockRepo.Object);
        var query = new GetHuntHistoryQuery("user-1", 100);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Status.Should().Be("Found");
        result[0].EventName.Should().Be("Rock Festival");
        result[1].Status.Should().Be("Cancelled");
        result[1].EventName.Should().Be("Jazz Night");
    }

    [Fact]
    public async Task GetRecentHuntsQueryHandler_ShouldReturnMappedActiveDtos()
    {
        // Arrange
        var mockRepo = new Mock<IHuntRequestRepository>();
        var sampleHunts = new List<HuntRequest>
        {
            new HuntRequest
            {
                Id = Guid.NewGuid(),
                UserId = "user-2",
                ProviderName = "ObiletLive",
                RouteOrEventId = "https://obilet.com/bus-1",
                EventName = "Ankara - Istanbul",
                Status = RequestStatus.Active,
                CreatedAt = new DateTime(2026, 10, 13, 14, 30, 0, DateTimeKind.Utc)
            }
        };

        mockRepo.Setup(r => r.GetRecentByUserIdAsync("user-2", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleHunts);

        var handler = new GetRecentHuntsQueryHandler(mockRepo.Object);
        var query = new GetRecentHuntsQuery("user-2", 50);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Provider.Should().Be("ObiletLive");
        result[0].EventName.Should().Be("Ankara - Istanbul");
        result[0].Status.Should().Be("Active");
    }
}
