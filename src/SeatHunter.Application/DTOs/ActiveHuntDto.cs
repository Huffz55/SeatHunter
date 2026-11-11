namespace SeatHunter.Application.DTOs;

public class ActiveHuntDto
{
    public string Id { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public decimal? MaxPrice { get; set; }
    public string? PreferredCategory { get; set; }
    public bool AutoReserveEnabled { get; set; }
}
