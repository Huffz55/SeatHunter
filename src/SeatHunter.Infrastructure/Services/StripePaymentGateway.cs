using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Infrastructure.Services;

/// <summary>
/// Gerçek Stripe Checkout Sessions API ve Stripe-Signature (t=...,v1=...) webhook doğrulayıcısı.
/// Billing:Provider=Stripe olduğunda DI tarafından aktif edilir.
/// </summary>
public class StripePaymentGateway : IPaymentGateway
{
    private const string StripeSessionsEndpoint = "https://api.stripe.com/v1/checkout/sessions";
    private const long DefaultWebhookToleranceSeconds = 300;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public StripePaymentGateway(
        HttpClient httpClient,
        IConfiguration configuration,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string ProviderName => "Stripe";

    public async Task<PaymentCheckoutSession> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var secretKey = _configuration["Billing:Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Stripe seçiliyken Billing:Stripe:SecretKey yapılandırılmalıdır.");
        }

        var unitAmount = ((long)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);

        var formFields = new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["customer_email"] = request.UserEmail,
            ["success_url"] = string.IsNullOrWhiteSpace(request.SuccessRedirectUrl)
                ? "https://localhost:5179/?billing=success"
                : request.SuccessRedirectUrl,
            ["cancel_url"] = string.IsNullOrWhiteSpace(request.CancelRedirectUrl)
                ? "https://localhost:5179/?billing=cancel"
                : request.CancelRedirectUrl,
            ["line_items[0][price_data][currency]"] = request.Currency.ToLowerInvariant(),
            ["line_items[0][price_data][unit_amount]"] = unitAmount,
            ["line_items[0][price_data][product_data][name]"] = $"SeatHunter {request.TargetTier} ({request.Cycle})",
            ["line_items[0][quantity]"] = "1",
            ["metadata[userId]"] = request.UserId,
            ["metadata[targetTier]"] = request.TargetTier.ToString(),
            ["metadata[cycle]"] = request.Cycle.ToString()
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, StripeSessionsEndpoint)
        {
            Content = new FormUrlEncodedContent(formFields)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            httpRequest.Headers.TryAddWithoutValidation("Idempotency-Key", request.IdempotencyKey);
        }

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Stripe Checkout oturumu oluşturulamadı ({(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var sessionId = root.GetProperty("id").GetString();
        var checkoutUrl = root.GetProperty("url").GetString();

        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(checkoutUrl))
        {
            throw new InvalidOperationException("Stripe Checkout yanıtında id veya url alanı eksik.");
        }

        return new PaymentCheckoutSession(
            Provider: ProviderName,
            ExternalSessionId: sessionId,
            CheckoutUrl: checkoutUrl
        );
    }

    public bool VerifyWebhookSignature(string payload, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader)) return false;

        var webhookSecret = _configuration["Billing:Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            webhookSecret = _configuration["Billing:WebhookSecret"];
        }
        if (string.IsNullOrWhiteSpace(webhookSecret)) return false;

        string? timestamp = null;
        var signatures = new List<string>();

        foreach (var part in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0) continue;

            var key = part[..idx];
            var value = part[(idx + 1)..];
            if (key == "t") timestamp = value;
            else if (key == "v1" && !string.IsNullOrWhiteSpace(value)) signatures.Add(value);
        }

        if (string.IsNullOrWhiteSpace(timestamp) || signatures.Count == 0) return false;
        if (!long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ts)) return false;

        var nowUnix = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        if (Math.Abs(nowUnix - ts) > DefaultWebhookToleranceSeconds) return false;

        var signedPayload = $"{timestamp}.{payload}";
        var expectedHex = ComputeStripeV1Signature(signedPayload, webhookSecret);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHex);

        foreach (var candidate in signatures)
        {
            var candidateBytes = Encoding.UTF8.GetBytes(candidate.ToLowerInvariant());
            if (expectedBytes.Length == candidateBytes.Length &&
                CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes))
            {
                return true;
            }
        }

        return false;
    }

    public static string ComputeStripeV1Signature(string signedPayload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
