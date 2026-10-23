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
    DateTime? ReservedUntil = null
);

public interface INotificationDispatcher
{
    Task DispatchTicketFoundAsync(TicketFoundNotification notification, CancellationToken cancellationToken = default);
}
