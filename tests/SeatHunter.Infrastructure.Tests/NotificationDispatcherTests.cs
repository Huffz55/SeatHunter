using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SeatHunter.Application.Interfaces;
using SeatHunter.Infrastructure.Services;
using Xunit;

namespace SeatHunter.Infrastructure.Tests;

public class NotificationDispatcherTests
{
    [Fact]
    public async Task DispatchTicketFoundAsync_ShouldInvokeAllChannelsConcurrently()
    {
        // Arrange
        var mockSignalR = new Mock<ISignalRNotifier>();
        var mockEmail = new Mock<IEmailService>();
        var mockTelegram = new Mock<ITelegramService>();
        var mockDiscord = new Mock<IDiscordService>();
        var mockMetrics = new Mock<IScraperMetrics>();
        var mockLogger = new Mock<ILogger<NotificationDispatcher>>();

        var dispatcher = new NotificationDispatcher(
            mockSignalR.Object,
            mockEmail.Object,
            mockTelegram.Object,
            mockDiscord.Object,
            mockMetrics.Object,
            mockLogger.Object
        );

        var notification = new TicketFoundNotification(
            RequestId: Guid.NewGuid(),
            UserId: "user-123",
            UserEmail: "test@example.com",
            ProviderName: "Biletix",
            RouteOrEventId: "https://biletix.com/tarkan",
            EventName: "Tarkan Konseri",
            Language: "tr"
        );

        // Act
        await dispatcher.DispatchTicketFoundAsync(notification);

        // Assert
        mockSignalR.Verify(s => s.SendSeatFoundAsync("user-123", notification.RequestId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        mockEmail.Verify(e => e.SendTicketFoundEmailAsync("test@example.com", "https://biletix.com/tarkan", "https://biletix.com/tarkan", "tr"), Times.Once);
        mockTelegram.Verify(t => t.SendMessageAsync(It.Is<string>(m => m.Contains("Tarkan Konseri")), It.IsAny<CancellationToken>()), Times.Once);
        mockDiscord.Verify(d => d.SendTicketFoundAlertAsync("Tarkan Konseri", "Biletix", "https://biletix.com/tarkan", "test@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchTicketFoundAsync_ShouldRemainResilient_WhenEmailServiceFails()
    {
        // Arrange
        var mockSignalR = new Mock<ISignalRNotifier>();
        var mockEmail = new Mock<IEmailService>();
        var mockTelegram = new Mock<ITelegramService>();
        var mockDiscord = new Mock<IDiscordService>();
        var mockMetrics = new Mock<IScraperMetrics>();
        var mockLogger = new Mock<ILogger<NotificationDispatcher>>();

        // E-posta servisi istisna fırlatıyor (örneğin SMTP sunucusu çöktü)
        mockEmail.Setup(e => e.SendTicketFoundEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection timed out"));

        var dispatcher = new NotificationDispatcher(
            mockSignalR.Object,
            mockEmail.Object,
            mockTelegram.Object,
            mockDiscord.Object,
            mockMetrics.Object,
            mockLogger.Object
        );

        var notification = new TicketFoundNotification(
            RequestId: Guid.NewGuid(),
            UserId: "user-999",
            UserEmail: "faulty@example.com",
            ProviderName: "Obilet",
            RouteOrEventId: "https://obilet.com/bus",
            EventName: "Istanbul - Izmir",
            Language: "tr"
        );

        // Act & Assert (Metot fırlatılan hatadan ötürü çökmemeli)
        var act = async () => await dispatcher.DispatchTicketFoundAsync(notification);
        await act.Should().NotThrowAsync();

        // Diğer tüm kanallar (SignalR, Telegram, Discord) yine de başarıyla çağrılmış olmalı
        mockSignalR.Verify(s => s.SendSeatFoundAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        mockTelegram.Verify(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        mockDiscord.Verify(d => d.SendTicketFoundAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
