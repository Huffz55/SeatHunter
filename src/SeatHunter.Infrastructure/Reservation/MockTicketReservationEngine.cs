using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;

namespace SeatHunter.Infrastructure.Reservation;

public class MockTicketReservationEngine : ITicketReservationEngine
{
    private readonly ILogger<MockTicketReservationEngine> _logger;

    public MockTicketReservationEngine(ILogger<MockTicketReservationEngine> logger)
    {
        _logger = logger;
    }

    public Task<ReservationResult> AttemptReserveAsync(HuntRequest hunt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🎫 [MockTicketReservationEngine] VIP otomatik sepete ekleme simüle ediliyor: {EventName} ({Route})", 
            hunt.EventName ?? hunt.RouteOrEventId, hunt.RouteOrEventId);

        var reservationToken = Guid.NewGuid().ToString("N");
        var expiresAt = DateTime.UtcNow.AddMinutes(10);
        
        // Simüle edilmiş doğrudan ödeme / sepet linki
        var cartUrl = hunt.RouteOrEventId.Contains('?')
            ? $"{hunt.RouteOrEventId}&vip_cart={reservationToken}&hold_until={expiresAt:yyyyMMddHHmmss}"
            : $"{hunt.RouteOrEventId}?vip_cart={reservationToken}&hold_until={expiresAt:yyyyMMddHHmmss}";

        _logger.LogInformation("✅ [MockTicketReservationEngine] Koltuk sepete eklendi ve 10 dakika boyunca rezerve edildi: {CartUrl}", cartUrl);

        return Task.FromResult(new ReservationResult(
            IsSuccess: true,
            CartUrl: cartUrl,
            ExpiresAt: expiresAt,
            FailureReason: null
        ));
    }
}
