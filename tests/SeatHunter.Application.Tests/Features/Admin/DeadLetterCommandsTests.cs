using FluentAssertions;
using Moq;
using SeatHunter.Application.Features.Admin.Commands.PurgeDeadLetters;
using SeatHunter.Application.Features.Admin.Commands.ReplayDeadLetters;
using SeatHunter.Application.Features.Admin.Queries.GetDeadLetterStatus;
using SeatHunter.Application.Interfaces;
using Xunit;

namespace SeatHunter.Application.Tests.Features.Admin;

public class DeadLetterCommandsTests
{
    private readonly Mock<IDeadLetterReplayService> _mockReplayService = new();

    [Fact]
    public async Task GetDeadLetterStatusQueryHandler_ShouldReturnStatus()
    {
        // Arrange
        var status = new DeadLetterQueueStatus("hunt_requests_dlq", 7, 1, DateTime.UtcNow);
        _mockReplayService.Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(status);

        var handler = new GetDeadLetterStatusQueryHandler(_mockReplayService.Object);

        // Act
        var result = await handler.Handle(new GetDeadLetterStatusQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.MessageCount.Should().Be(7u);
        result.QueueName.Should().Be("hunt_requests_dlq");
    }

    [Fact]
    public async Task ReplayDeadLettersCommandHandler_ShouldReturnReplayedCount()
    {
        // Arrange
        _mockReplayService.Setup(s => s.ReplayMessagesAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(15);

        var handler = new ReplayDeadLettersCommandHandler(_mockReplayService.Object);

        // Act
        var result = await handler.Handle(new ReplayDeadLettersCommand(50), CancellationToken.None);

        // Assert
        result.Should().Be(15);
        _mockReplayService.Verify(s => s.ReplayMessagesAsync(50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurgeDeadLettersCommandHandler_ShouldReturnPurgedCount()
    {
        // Arrange
        _mockReplayService.Setup(s => s.PurgeQueueAsync(It.IsAny<CancellationToken>())).ReturnsAsync(25u);

        var handler = new PurgeDeadLettersCommandHandler(_mockReplayService.Object);

        // Act
        var result = await handler.Handle(new PurgeDeadLettersCommand(), CancellationToken.None);

        // Assert
        result.Should().Be(25u);
        _mockReplayService.Verify(s => s.PurgeQueueAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
