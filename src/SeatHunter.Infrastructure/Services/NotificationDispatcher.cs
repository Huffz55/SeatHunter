using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Infrastructure.Services;

public class NotificationDispatcher : INotificationDispatcher
{
    private readonly ISignalRNotifier _signalRNotifier;
    private readonly IEmailService _emailService;
    private readonly ITelegramService _telegramService;
    private readonly IDiscordService _discordService;
    private readonly IScraperMetrics _metrics;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        ISignalRNotifier signalRNotifier,
        IEmailService emailService,
        ITelegramService telegramService,
        IDiscordService discordService,
        IScraperMetrics metrics,
        ILogger<NotificationDispatcher> logger)
    {
        _signalRNotifier = signalRNotifier;
        _emailService = emailService;
        _telegramService = telegramService;
        _discordService = discordService;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task DispatchTicketFoundAsync(TicketFoundNotification notification, CancellationToken cancellationToken = default)
    {
        var eventTitle = !string.IsNullOrWhiteSpace(notification.EventName) 
            ? notification.EventName 
            : notification.RouteOrEventId;

        _logger.LogInformation("🚀 Çok kanallı bildirim dağıtımı başlatılıyor - İstek: {RequestId}, Kullanıcı: {UserId}", notification.RequestId, notification.UserId);

        var tasks = new List<Task>
        {
            SendSignalRAsync(notification, eventTitle, cancellationToken),
            SendEmailAsync(notification, cancellationToken),
            SendTelegramAsync(notification, eventTitle, cancellationToken),
            SendDiscordAsync(notification, eventTitle, cancellationToken)
        };

        await Task.WhenAll(tasks);
        _logger.LogInformation("✅ Çok kanallı bildirim dağıtımı tamamlandı - İstek: {RequestId}", notification.RequestId);
    }

    private async Task SendSignalRAsync(TicketFoundNotification notification, string eventTitle, CancellationToken cancellationToken)
    {
        try
        {
            var msg = notification.IsAutoReserved && !string.IsNullOrEmpty(notification.ReservationCartUrl)
                ? $"⚡ VIP REZERVASYON: {notification.ProviderName} üzerinde {eventTitle} koltuğunuz sepete eklendi! Satın al: {notification.ReservationCartUrl}"
                : $"{notification.ProviderName} üzerinde {eventTitle} rotası için boş koltuk yakalandı!";
            await _signalRNotifier.SendSeatFoundAsync(notification.UserId, notification.RequestId, msg, cancellationToken);
            _metrics.RecordNotificationDispatched("signalr");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR bildirimi gönderilirken hata oluştu - İstek: {RequestId}", notification.RequestId);
        }
    }

    private async Task SendEmailAsync(TicketFoundNotification notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(notification.UserEmail)) return;

        try
        {
            var targetLink = notification.IsAutoReserved && !string.IsNullOrEmpty(notification.ReservationCartUrl)
                ? notification.ReservationCartUrl
                : notification.RouteOrEventId;

            await _emailService.SendTicketFoundEmailAsync(
                notification.UserEmail, 
                targetLink, 
                targetLink, 
                notification.Language
            );
            _metrics.RecordNotificationDispatched("email");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "E-posta bildirimi gönderilirken hata oluştu: {Email}", notification.UserEmail);
        }
    }

    private async Task SendTelegramAsync(TicketFoundNotification notification, string eventTitle, CancellationToken cancellationToken)
    {
        try
        {
            var telegramMessage = notification.IsAutoReserved && !string.IsNullOrEmpty(notification.ReservationCartUrl)
                ? $"⚡ VIP REZERVASYON ALARMI!\n{notification.UserEmail ?? notification.UserId} için {notification.ProviderName} ({eventTitle}) koltuğu otomatik olarak sepete eklendi.\n⏱️ 10 dakika boyunca adınıza rezerve edildi.\n🛒 Doğrudan Ödeme Linki: {notification.ReservationCartUrl}"
                : $"🚀 Sistem Raporu: {notification.UserEmail ?? notification.UserId} kullanıcısı için {notification.ProviderName} üzerinde {eventTitle} bileti başarıyla bulundu! 🔗 {notification.RouteOrEventId}";
            await _telegramService.SendMessageAsync(telegramMessage, cancellationToken);
            _metrics.RecordNotificationDispatched("telegram");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram bildirimi gönderilirken hata oluştu.");
        }
    }

    private async Task SendDiscordAsync(TicketFoundNotification notification, string eventTitle, CancellationToken cancellationToken)
    {
        try
        {
            var actionUrl = notification.IsAutoReserved && !string.IsNullOrEmpty(notification.ReservationCartUrl)
                ? notification.ReservationCartUrl
                : notification.RouteOrEventId;

            var titleWithVip = notification.IsAutoReserved
                ? $"⚡ [VIP REZERVE EDİLDİ] {eventTitle}"
                : eventTitle;

            await _discordService.SendTicketFoundAlertAsync(
                titleWithVip, 
                notification.ProviderName, 
                actionUrl, 
                notification.UserEmail, 
                cancellationToken
            );
            _metrics.RecordNotificationDispatched("discord");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discord bildirimi gönderilirken hata oluştu.");
        }
    }
}
