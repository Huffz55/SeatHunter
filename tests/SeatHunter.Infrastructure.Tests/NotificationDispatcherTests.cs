using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
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

    [Fact]
    public async Task DispatchTicketFoundAsync_ShouldFormatPriceDropAlert_WhenIsPriceDropIsTrue()
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
            UserId: "user-deal-hunter",
            UserEmail: "hunter@example.com",
            ProviderName: "Biletix",
            RouteOrEventId: "https://biletix.com/coldplay",
            EventName: "Coldplay Konseri",
            Language: "tr",
            ObservedPrice: 850m,
            IsPriceDrop: true,
            PriceDropPercentage: 25.5m,
            LowestHistoricalPrice: 850m
        );

        // Act
        await dispatcher.DispatchTicketFoundAsync(notification);

        // Assert
        // 1. SignalR message mentions price drop and discount percent
        mockSignalR.Verify(s => s.SendSeatFoundAsync(
            "user-deal-hunter",
            notification.RequestId,
            It.Is<string>(msg => msg.Contains("📉 FİYAT DÜŞÜŞÜ") && msg.Contains("850") && msg.Contains(notification.PriceDropPercentage.ToString()!)),
            It.IsAny<CancellationToken>()
        ), Times.Once);

        // 2. Telegram message mentions price drop alert
        mockTelegram.Verify(t => t.SendMessageAsync(
            It.Is<string>(msg => msg.Contains("📉 FİYAT DÜŞÜŞÜ ALARMI!") && msg.Contains("850 TL")),
            It.IsAny<CancellationToken>()
        ), Times.Once);

        // 3. Discord title contains discount badge
        mockDiscord.Verify(d => d.SendTicketFoundAlertAsync(
            It.Is<string>(title => title.Contains("📉 [FİYAT İNDİRİMİ") && title.Contains("Coldplay Konseri")),
            "Biletix",
            "https://biletix.com/coldplay",
            "hunter@example.com",
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task DispatchTicketFoundAsync_ShouldPersistUserNotification_WhenRepositoryIsProvided()
    {
        var mockSignalR = new Mock<ISignalRNotifier>();
        var mockEmail = new Mock<IEmailService>();
        var mockTelegram = new Mock<ITelegramService>();
        var mockDiscord = new Mock<IDiscordService>();
        var mockMetrics = new Mock<IScraperMetrics>();
        var mockLogger = new Mock<ILogger<NotificationDispatcher>>();
        var mockNotificationRepo = new Mock<INotificationRepository>();

        var dispatcher = new NotificationDispatcher(
            mockSignalR.Object,
            mockEmail.Object,
            mockTelegram.Object,
            mockDiscord.Object,
            mockMetrics.Object,
            mockLogger.Object,
            notificationRepository: mockNotificationRepo.Object
        );

        var notification = new TicketFoundNotification(
            RequestId: Guid.NewGuid(),
            UserId: "user-persist",
            UserEmail: "persist@example.com",
            ProviderName: "Universal",
            RouteOrEventId: "https://example.com/show",
            EventName: "Senfoni Gecesi",
            Language: "tr",
            ObservedPrice: 450m
        );

        await dispatcher.DispatchTicketFoundAsync(notification);

        mockNotificationRepo.Verify(r => r.AddAsync(
            It.Is<UserNotification>(n =>
                n.UserId == "user-persist" &&
                n.Title == "Bilet Bulundu!" &&
                n.Type == "success" &&
                n.Message.Contains("Senfoni Gecesi") &&
                n.RelatedUrl == "https://example.com/show"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchTicketFoundAsync_ShouldStillDispatchChannels_WhenNotificationRepositoryThrows()
    {
        var mockSignalR = new Mock<ISignalRNotifier>();
        var mockEmail = new Mock<IEmailService>();
        var mockTelegram = new Mock<ITelegramService>();
        var mockDiscord = new Mock<IDiscordService>();
        var mockMetrics = new Mock<IScraperMetrics>();
        var mockLogger = new Mock<ILogger<NotificationDispatcher>>();
        var mockNotificationRepo = new Mock<INotificationRepository>();

        mockNotificationRepo
            .Setup(r => r.AddAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB transient error"));

        var dispatcher = new NotificationDispatcher(
            mockSignalR.Object,
            mockEmail.Object,
            mockTelegram.Object,
            mockDiscord.Object,
            mockMetrics.Object,
            mockLogger.Object,
            notificationRepository: mockNotificationRepo.Object
        );

        var notification = new TicketFoundNotification(
            RequestId: Guid.NewGuid(),
            UserId: "user-resilient",
            UserEmail: "resilient@example.com",
            ProviderName: "Universal",
            RouteOrEventId: "https://example.com/resilient",
            EventName: "Dayanıklı Etkinlik",
            Language: "tr"
        );

        var act = async () => await dispatcher.DispatchTicketFoundAsync(notification);
        await act.Should().NotThrowAsync();

        mockSignalR.Verify(s => s.SendSeatFoundAsync("user-resilient", notification.RequestId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        mockEmail.Verify(e => e.SendTicketFoundEmailAsync("resilient@example.com", "https://example.com/resilient", "https://example.com/resilient", "tr"), Times.Once);
    }
}
