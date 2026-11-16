namespace SeatHunter.Application.Interfaces;

public record TicketFoundNotification(
    Guid RequestId,
    string UserId,
    string? UserEmail,
    string ProviderName,
    string RouteOrEventId,
    string? EventName,
    string Language = "tr",
    bool IsAutoReserved = false,
    string? ReservationCartUrl = null,
    DateTime? ReservedUntil = null,
    decimal? ObservedPrice = null,
    bool IsPriceDrop = false,
    decimal? PriceDropPercentage = null,
    decimal? LowestHistoricalPrice = null,
    bool NotifyEmail = true,
    bool NotifyTelegram = true,
    bool NotifyDiscord = true,
    string? TelegramChatId = null,
    string? DiscordWebhookUrl = null
);

public interface INotificationDispatcher
{
    Task DispatchTicketFoundAsync(TicketFoundNotification notification, CancellationToken cancellationToken = default);
}
