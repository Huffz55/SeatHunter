using SeatHunter.Domain.Enums;

namespace SeatHunter.Domain.Entities;

/// <summary>
/// Kullanıcının ücretli abonelik dönemini ve otomatik yenileme durumunu tutar.
/// </summary>
public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public UserTier PlanTier { get; set; } = UserTier.Pro;
    public BillingCycle Cycle { get; set; } = BillingCycle.Monthly;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public string Provider { get; set; } = "Simulated";
    public string? ExternalSubscriptionId { get; set; }
    public DateTime CurrentPeriodStartUtc { get; set; } = DateTime.UtcNow;
    public DateTime CurrentPeriodEndUtc { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
