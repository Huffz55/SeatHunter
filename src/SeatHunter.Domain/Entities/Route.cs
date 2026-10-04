namespace SeatHunter.Domain.Entities;

public class Route
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DepartureCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty; // e.g., "TurkishAirlines", "KamilKoc"
    public bool IsActive { get; set; } = true;
}
