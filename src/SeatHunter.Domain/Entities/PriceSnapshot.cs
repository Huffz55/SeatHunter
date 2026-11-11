namespace SeatHunter.Domain.Entities;

public class PriceSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RouteOrEventId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string? EventName { get; set; }
    public string? CategoryName { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
