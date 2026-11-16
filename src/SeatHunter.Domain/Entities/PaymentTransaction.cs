using SeatHunter.Domain.Enums;

namespace SeatHunter.Domain.Entities;

/// <summary>
/// Ödeme oturumu ve idempotent webhook işleme kaydı.
/// </summary>
public class PaymentTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public Guid? SubscriptionId { get; set; }
    public UserTier TargetTier { get; set; } = UserTier.Pro;
    public BillingCycle Cycle { get; set; } = BillingCycle.Monthly;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;
    public string Provider { get; set; } = "Simulated";
    public string ExternalSessionId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}
