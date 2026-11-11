using Microsoft.Extensions.Logging;
using Moq;
using SeatHunter.Application.Interfaces;
using SeatHunter.Infrastructure.Services;
using Xunit;

namespace SeatHunter.Infrastructure.Tests;

public class NotificationDispatcherAdminIntegrationTests
{
    private readonly Mock<ISignalRNotifier> _mockSignalR = new();
    private readonly Mock<IEmailService> _mockEmail = new();
    private readonly Mock<ITelegramService> _mockTelegram = new();
    private readonly Mock<IDiscordService> _mockDiscord = new();
    private readonly Mock<IScraperMetrics> _mockMetrics = new();
    private readonly Mock<ILogger<NotificationDispatcher>> _mockLogger = new();
    private readonly Mock<IAdminNotifier> _mockAdminNotifier = new();

    [Fact]
    public async Task DispatchTicketFoundAsync_ShouldBroadcastToAdminStream_WhenAdminNotifierIsPresent()
    {
        // Arrange
        var dispatcher = new NotificationDispatcher(
            _mockSignalR.Object,
            _mockEmail.Object,
            _mockTelegram.Object,
            _mockDiscord.Object,
            _mockMetrics.Object,
            _mockLogger.Object,
            _mockAdminNotifier.Object
        );

        var notification = new TicketFoundNotification(
            RequestId: Guid.NewGuid(),
            UserId: "user-admin-test",
            UserEmail: "admin-test@example.com",
            ProviderName: "Biletix",
            RouteOrEventId: "https://biletix.com/derbi",
            EventName: "Derbi Maçı",
            Language: "tr"
        );

        // Act
        await dispatcher.DispatchTicketFoundAsync(notification);

        // Assert
        _mockAdminNotifier.Verify(a => a.BroadcastSeatDiscoveredAsync(
            notification, 
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
}
