using SeatHunter.Domain.Enums;
namespace SeatHunter.Domain.Entities;
public class HuntRequest {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string RouteOrEventId { get; set; } = string.Empty;
    public DateTime TargetDate { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextScanTime { get; set; } = DateTime.UtcNow;
    public string Language { get; set; } = "tr";
    public string? EventName { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? PreferredCategory { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool AutoReserveEnabled { get; set; } = false;
    public DateTime? ReservedUntil { get; set; }
    public string? ReservationCartUrl { get; set; }
}
