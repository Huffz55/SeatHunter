using FluentAssertions;
using Moq;
using SeatHunter.Application.Features.HuntRequests.Commands.CancelHuntRequest;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Enums;
using Xunit;

namespace SeatHunter.Application.Tests.Features.HuntRequests;

public class CancelHuntRequestCommandHandlerTests
{
    private readonly Mock<IHuntRequestRepository> _mockRepo;
    private readonly Mock<IHuntManagerService> _mockHuntManager;
    private readonly Mock<IDistributedHuntScheduler> _mockScheduler;
    private readonly CancelHuntRequestCommandHandler _handler;

    public CancelHuntRequestCommandHandlerTests()
    {
        _mockRepo = new Mock<IHuntRequestRepository>();
        _mockHuntManager = new Mock<IHuntManagerService>();
        _mockScheduler = new Mock<IDistributedHuntScheduler>();

        _handler = new CancelHuntRequestCommandHandler(
            _mockRepo.Object,
            _mockHuntManager.Object,
            _mockScheduler.Object
        );
    }

    [Fact]
    public async Task Handle_ShouldCancelHunt_AndRemoveFromScheduler_WhenValidUser()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var userId = "valid-user";
        var hunt = new HuntRequest
        {
            Id = requestId,
            UserId = userId,
            Status = RequestStatus.Active
        };

        _mockRepo.Setup(r => r.GetByIdAsync(requestId, It.IsAny<CancellationToken>())).ReturnsAsync(hunt);

        var command = new CancelHuntRequestCommand(requestId, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        hunt.Status.Should().Be(RequestStatus.Cancelled);
        _mockRepo.Verify(r => r.UpdateAsync(hunt, It.IsAny<CancellationToken>()), Times.Once);
        _mockHuntManager.Verify(m => m.CancelHunt(requestId.ToString()), Times.Once);
        _mockScheduler.Verify(s => s.RemoveHuntAsync(requestId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnFalse_WhenHuntBelongsToDifferentUser_IdorProtection()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var hunt = new HuntRequest
        {
            Id = requestId,
            UserId = "user-original",
            Status = RequestStatus.Active
        };

        _mockRepo.Setup(r => r.GetByIdAsync(requestId, It.IsAny<CancellationToken>())).ReturnsAsync(hunt);

        var command = new CancelHuntRequestCommand(requestId, "malicious-user");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        hunt.Status.Should().Be(RequestStatus.Active);
        _mockRepo.Verify(r => r.UpdateAsync(It.IsAny<HuntRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockScheduler.Verify(s => s.RemoveHuntAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
