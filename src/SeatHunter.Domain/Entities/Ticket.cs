namespace SeatHunter.Domain.Entities;

public class Ticket
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RouteId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime DepartureTime { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
}
