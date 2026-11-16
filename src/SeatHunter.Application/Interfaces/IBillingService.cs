using SeatHunter.Application.Common;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Application.Interfaces;

public record StartCheckoutCommand(
    UserTier TargetTier,
    BillingCycle Cycle,
    string? IdempotencyKey = null,
    string? SuccessRedirectUrl = null,
    string? CancelRedirectUrl = null
);

public record CheckoutSessionResult(
    Guid TransactionId,
    string ExternalSessionId,
    string Provider,
    string CheckoutUrl,
    string TargetTier,
    string Cycle,
    decimal Amount,
    string Currency
);

public record BillingWebhookEvent(
    string EventType,
    string ExternalSessionId,
    string? ExternalSubscriptionId = null,
    string? FailureReason = null
);

public record ActiveSubscriptionDto(
    Guid Id,
    string PlanTier,
    string Cycle,
    string Status,
    string Provider,
    DateTime CurrentPeriodStartUtc,
    DateTime CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd,
    DateTime? CancelledAtUtc
);

public record BillingTransactionDto(
    Guid Id,
    string TargetTier,
    string Cycle,
    decimal Amount,
    string Currency,
    string Status,
    string Provider,
    string ExternalSessionId,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc
);

public record UserBillingStatusDto(
    string EffectiveTier,
    DateTime? SubscriptionExpiresAtUtc,
    ActiveSubscriptionDto? ActiveSubscription,
    IReadOnlyList<BillingTransactionDto> Transactions
);

public record SubscriptionMutationResult(
    bool Succeeded,
    string EffectiveTier,
    DateTime? SubscriptionExpiresAtUtc,
    string Message
);

public interface IBillingService
{
    IReadOnlyList<PlanDefinition> GetPlansCatalog();
    Task<UserBillingStatusDto> GetUserBillingStatusAsync(string userId, CancellationToken cancellationToken = default);
    Task<CheckoutSessionResult> StartCheckoutAsync(string userId, StartCheckoutCommand command, CancellationToken cancellationToken = default);
    Task<SubscriptionMutationResult> ConfirmSandboxCheckoutAsync(string userId, string externalSessionId, CancellationToken cancellationToken = default);
    Task<SubscriptionMutationResult> ProcessWebhookAsync(BillingWebhookEvent webhookEvent, string rawPayload, string? signatureHeader, CancellationToken cancellationToken = default);
    Task<SubscriptionMutationResult> CancelAutoRenewAsync(string userId, CancellationToken cancellationToken = default);
    Task<int> ExpireDueSubscriptionsAsync(CancellationToken cancellationToken = default);
}
