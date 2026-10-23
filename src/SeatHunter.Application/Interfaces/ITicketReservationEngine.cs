using SeatHunter.Domain.Entities;

namespace SeatHunter.Application.Interfaces;

public record ReservationResult(
    bool IsSuccess, 
    string? CartUrl, 
    DateTime? ExpiresAt, 
    string? FailureReason = null
);

public interface ITicketReservationEngine
{
    Task<ReservationResult> AttemptReserveAsync(
        HuntRequest hunt, 
        CancellationToken cancellationToken = default);
}
