using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Infrastructure.Reservation;
using SeatHunter.Infrastructure.Services;
using Xunit;

namespace SeatHunter.Infrastructure.Tests;

public class TicketReservationEngineTests
{
    private readonly Mock<ILogger<MockTicketReservationEngine>> _mockLogger = new();
    private readonly Mock<ISignalRNotifier> _mockSignalR = new();
    private readonly Mock<IEmailService> _mockEmail = new();
    private readonly Mock<ITelegramService> _mockTelegram = new();
    private readonly Mock<IDiscordService> _mockDiscord = new();
    private readonly Mock<IScraperMetrics> _mockMetrics = new();
    private readonly Mock<ILogger<NotificationDispatcher>> _mockDispatcherLogger = new();

    [Fact]
    public async Task MockTicketReservationEngine_ShouldReserveTicket_AndReturnCartUrl()
    {
        // Arrange
        var engine = new MockTicketReservationEngine(_mockLogger.Object);
        var hunt = new HuntRequest
        {
            Id = Guid.NewGuid(),
            UserId = "user-vip",
            RouteOrEventId = "https://biletix.com/etkinlik/12345",
            EventName = "Metallica Konseri",
            AutoReserveEnabled = true
        };

        // Act
        var result = await engine.AttemptReserveAsync(hunt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.CartUrl.Should().NotBeNullOrWhiteSpace();
        result.CartUrl.Should().Contain("vip_cart=");
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(9));
        result.ExpiresAt.Should().BeBefore(DateTime.UtcNow.AddMinutes(11));
    }

    [Fact]
    public async Task NotificationDispatcher_WhenAutoReserved_ShouldIncludeVipDetailsInAllChannels()
    {
        // Arrange
        var dispatcher = new NotificationDispatcher(
            _mockSignalR.Object,
            _mockEmail.Object,
            _mockTelegram.Object,
            _mockDiscord.Object,
            _mockMetrics.Object,
            _mockDispatcherLogger.Object
        );

        var cartUrl = "https://biletix.com/checkout?vip_cart=token123";
        var expiresAt = DateTime.UtcNow.AddMinutes(10);
        var notification = new TicketFoundNotification(
            RequestId: Guid.NewGuid(),
            UserId: "user-vip",
            UserEmail: "vip@example.com",
            ProviderName: "Biletix",
            RouteOrEventId: "https://biletix.com/event/999",
            EventName: "Final Derbisi",
            Language: "tr",
            IsAutoReserved: true,
            ReservationCartUrl: cartUrl,
            ReservedUntil: expiresAt
        );

        // Act
        await dispatcher.DispatchTicketFoundAsync(notification);

        // Assert
        // 1. SignalR should receive VIP-formatted message with cart URL
        _mockSignalR.Verify(s => s.SendSeatFoundAsync(
            "user-vip",
            notification.RequestId,
            It.Is<string>(msg => msg.Contains("⚡ VIP REZERVASYON") && msg.Contains(cartUrl)),
            It.IsAny<CancellationToken>()
        ), Times.Once);

        // 2. Email should be sent to user email with cart URL
        _mockEmail.Verify(e => e.SendTicketFoundEmailAsync(
            "vip@example.com",
            cartUrl,
            cartUrl,
            "tr"
        ), Times.Once);

        // 3. Telegram should receive VIP alarm with direct cart link
        _mockTelegram.Verify(t => t.SendMessageAsync(
            It.Is<string>(msg => msg.Contains("⚡ VIP REZERVASYON ALARMI!") && msg.Contains(cartUrl)),
            It.IsAny<CancellationToken>()
        ), Times.Once);

        // 4. Discord should receive VIP banner in title and cart URL
        _mockDiscord.Verify(d => d.SendTicketFoundAlertAsync(
            It.Is<string>(title => title.Contains("⚡ [VIP REZERVE EDİLDİ]")),
            "Biletix",
            cartUrl,
            "vip@example.com",
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
}
