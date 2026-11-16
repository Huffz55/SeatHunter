using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Infrastructure.Services;

/// <summary>
/// Geliştirme/Sandbox ödeme sağlayıcısı ve HMAC-SHA256 webhook imza doğrulayıcısı.
/// Gerçek Stripe/iyzico adaptörleri aynı IPaymentGateway sözleşmesiyle değiştirilebilir.
/// </summary>
public class SimulatedPaymentGateway : IPaymentGateway
{
    private const string DevFallbackWebhookSecret = "SeatHunterDevBillingWebhookSecret_DoNotUseInProduction_32Bytes!";
    private readonly IConfiguration _configuration;

    public SimulatedPaymentGateway(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ProviderName => "Simulated";

    public Task<PaymentCheckoutSession> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var sessionId = $"sim_cs_{Guid.NewGuid():N}";
        var checkoutUrl = $"/api/billing/sandbox/confirm/{sessionId}";

        return Task.FromResult(new PaymentCheckoutSession(
            Provider: ProviderName,
            ExternalSessionId: sessionId,
            CheckoutUrl: checkoutUrl
        ));
    }

    public bool VerifyWebhookSignature(string payload, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader)) return false;

        var secret = ResolveWebhookSecret();
        var expectedHex = ComputeSignature(payload, secret);
        var normalizedProvided = signatureHeader.Trim();

        // "sha256=<hex>" veya doğrudan "<hex>" formatlarını destekle
        if (normalizedProvided.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            normalizedProvided = normalizedProvided["sha256=".Length..];
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedHex.ToLowerInvariant());
        var providedBytes = Encoding.UTF8.GetBytes(normalizedProvided.ToLowerInvariant());

        return expectedBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    public static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string ResolveWebhookSecret()
    {
        var configured = _configuration["Billing:WebhookSecret"];
        if (!string.IsNullOrWhiteSpace(configured)) return configured;

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Production ortamında Billing:WebhookSecret yapılandırılmalıdır.");
        }

        return DevFallbackWebhookSecret;
    }
}
