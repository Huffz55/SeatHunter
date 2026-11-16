using SeatHunter.Domain.Enums;

namespace SeatHunter.Application.Interfaces;

public record CreateCheckoutSessionRequest(
    string UserId,
    string UserEmail,
    UserTier TargetTier,
    BillingCycle Cycle,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    string? SuccessRedirectUrl,
    string? CancelRedirectUrl
);

public record PaymentCheckoutSession(
    string Provider,
    string ExternalSessionId,
    string CheckoutUrl
);

/// <summary>
/// Ödeme sağlayıcısı (iyzico, Stripe veya Sandbox/Simulated) soyutlaması.
/// </summary>
public interface IPaymentGateway
{
    string ProviderName { get; }
    Task<PaymentCheckoutSession> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, CancellationToken cancellationToken = default);
    bool VerifyWebhookSignature(string payload, string? signatureHeader);
}
