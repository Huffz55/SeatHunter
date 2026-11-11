using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;

namespace SeatHunter.Infrastructure.Services;

public class NotificationDispatcher : INotificationDispatcher
{
    private readonly ISignalRNotifier _signalRNotifier;
    private readonly IEmailService _emailService;
    private readonly ITelegramService _telegramService;
    private readonly IDiscordService _discordService;
    private readonly IScraperMetrics _metrics;
    private readonly ILogger<NotificationDispatcher> _logger;
    private readonly IAdminNotifier? _adminNotifier;
    private readonly INotificationRepository? _notificationRepository;

    public NotificationDispatcher(
        ISignalRNotifier signalRNotifier,
        IEmailService emailService,
        ITelegramService telegramService,
        IDiscordService discordService,
        IScraperMetrics metrics,
        ILogger<NotificationDispatcher> logger,
        IAdminNotifier? adminNotifier = null,
        INotificationRepository? notificationRepository = null)
    {
        _signalRNotifier = signalRNotifier;
        _emailService = emailService;
        _telegramService = telegramService;
        _discordService = discordService;
        _metrics = metrics;
        _logger = logger;
        _adminNotifier = adminNotifier;
        _notificationRepository = notificationRepository;
    }

    public async Task DispatchTicketFoundAsync(TicketFoundNotification notification, CancellationToken cancellationToken = default)
    {
        var eventTitle = !string.IsNullOrWhiteSpace(notification.EventName) 
            ? notification.EventName 
            : notification.RouteOrEventId;

        _logger.LogInformation("🚀 Çok kanallı bildirim dağıtımı başlatılıyor - İstek: {RequestId}, Kullanıcı: {UserId}", notification.RequestId, notification.UserId);

        if (_notificationRepository != null && !string.IsNullOrWhiteSpace(notification.UserId))
        {
            try
            {
                var targetUrl = notification.IsAutoReserved && !string.IsNullOrEmpty(notification.ReservationCartUrl)
                    ? notification.ReservationCartUrl
                    : notification.RouteOrEventId;

                var notifMessage = notification.IsPriceDrop
                    ? $"{eventTitle} bilet fiyatı {notification.ObservedPrice} TL seviyesine düştü."
                    : notification.IsAutoReserved && !string.IsNullOrEmpty(notification.ReservationCartUrl)
                        ? $"{eventTitle} için koltuk otomatik olarak sepete eklendi."
                        : notification.ObservedPrice.HasValue
                            ? $"{eventTitle} için uygun bilet bulundu (Fiyat: {notification.ObservedPrice} TL)."
                            : $"{eventTitle} için uygun bilet bulundu.";

                await _notificationRepository.AddAsync(new UserNotification
                {
                    UserId = notification.UserId,
                    Title = notification.IsPriceDrop ? "Fiyat Düşüşü" : "Bilet Bulundu!",
                    Message = notifMessage,
                    Type = "success",
                    RelatedUrl = targetUrl
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bildirim veritabanına kaydedilemedi - İstek: {RequestId}", notification.RequestId);
            }
        }

        var tasks = new List<Task>
        {
            SendSignalRAsync(notification, eventTitle, cancellationToken),
            SendEmailAsync(notification, cancellationToken),
            SendTelegramAsync(notification, eventTitle, cancellationToken),
            SendDiscordAsync(notification, eventTitle, cancellationToken)
        };

        if (_adminNotifier != null)
        {
            tasks.Add(_adminNotifier.BroadcastSeatDiscoveredAsync(notification, cancellationToken));
        }

        await Task.WhenAll(tasks);
        _logger.LogInformation("✅ Çok kanallı bildirim dağıtımı tamamlandı - İstek: {RequestId}", notification.RequestId);
    }

    private async Task SendSignalRAsync(TicketFoundNotification notification, string eventTitle, CancellationToken cancellationToken)
    {
        try
        {
            string msg;
            if (notification.IsPriceDrop)
            {
                msg = $"📉 FİYAT DÜŞÜŞÜ: {eventTitle} bilet fiyatı {notification.ObservedPrice} TL'ye düştü! (%{notification.PriceDropPercentage} İndirim)";
            }
            else if (notification.IsAutoReserved && !string.IsNullOrEmpty(notification.ReservationCartUrl))
            {
                msg = $"⚡ VIP REZERVASYON: {notification.ProviderName} üzerinde {eventTitle} koltuğunuz sepete eklendi! Satın al: {notification.ReservationCartUrl}";
            }
            else
            {
                msg = notification.ObservedPrice.HasValue
                    ? $"{notification.ProviderName} üzerinde {eventTitle} rotası için boş koltuk yakalandı! (Fiyat: {notification.ObservedPrice} TL)"
                    : $"{notification.ProviderName} üzerinde {eventTitle} rotası için boş koltuk yakalandı!";
            }

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
            string telegramMessage;
            if (notification.IsPriceDrop)
            {
                telegramMessage = $"📉 FİYAT DÜŞÜŞÜ ALARMI!\n{notification.UserEmail ?? notification.UserId} için {notification.ProviderName} ({eventTitle}) biletinde fiyat indirimi tespit edildi!\n💰 Yeni Fiyat: {notification.ObservedPrice} TL (İndirim: %{notification.PriceDropPercentage})\n🔗 {notification.RouteOrEventId}";
            }
            else if (notification.IsAutoReserved && !string.IsNullOrEmpty(notification.ReservationCartUrl))
            {
                telegramMessage = $"⚡ VIP REZERVASYON ALARMI!\n{notification.UserEmail ?? notification.UserId} için {notification.ProviderName} ({eventTitle}) koltuğu otomatik olarak sepete eklendi.\n⏱️ 10 dakika boyunca adınıza rezerve edildi.\n🛒 Doğrudan Ödeme Linki: {notification.ReservationCartUrl}";
            }
            else
            {
                var priceText = notification.ObservedPrice.HasValue ? $" 💰 Fiyat: {notification.ObservedPrice} TL" : string.Empty;
                telegramMessage = $"🚀 Sistem Raporu: {notification.UserEmail ?? notification.UserId} kullanıcısı için {notification.ProviderName} üzerinde {eventTitle} bileti başarıyla bulundu!{priceText} 🔗 {notification.RouteOrEventId}";
            }

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

            string titleWithVip;
            if (notification.IsPriceDrop)
            {
                titleWithVip = $"📉 [FİYAT İNDİRİMİ %{notification.PriceDropPercentage}] {eventTitle} ({notification.ObservedPrice} TL)";
            }
            else if (notification.IsAutoReserved)
            {
                titleWithVip = $"⚡ [VIP REZERVE EDİLDİ] {eventTitle}";
            }
            else
            {
                titleWithVip = notification.ObservedPrice.HasValue 
                    ? $"{eventTitle} ({notification.ObservedPrice} TL)" 
                    : eventTitle;
            }

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
