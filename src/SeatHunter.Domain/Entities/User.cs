namespace SeatHunter.Domain.Entities;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Enums.UserTier Tier { get; set; } = Enums.UserTier.Free;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
