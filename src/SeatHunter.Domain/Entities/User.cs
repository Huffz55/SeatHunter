namespace SeatHunter.Domain.Entities;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Enums.UserTier Tier { get; set; } = Enums.UserTier.Free;
    public DateTime? SubscriptionExpiresAtUtc { get; set; }
    public string Role { get; set; } = "User"; // "Admin", "User"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Kullanıcı bazlı bildirim kanalı tercihleri
    public bool NotifyEmail { get; set; } = true;
    public bool NotifyTelegram { get; set; } = true;
    public bool NotifyDiscord { get; set; } = true;
    public string? TelegramChatId { get; set; }
    public string? DiscordWebhookUrl { get; set; }

    /// <summary>
    /// Süresi dolmuş ücretli abonelikleri çalışma zamanında güvenli şekilde Free kata düşürür.
    /// </summary>
    public Enums.UserTier GetEffectiveTier(DateTime? utcNow = null)
    {
        if (Tier != Enums.UserTier.Free &&
            SubscriptionExpiresAtUtc.HasValue &&
            SubscriptionExpiresAtUtc.Value <= (utcNow ?? DateTime.UtcNow))
        {
            return Enums.UserTier.Free;
        }

        return Tier;
    }
}
