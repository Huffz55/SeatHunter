using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Enums;
using SeatHunter.Infrastructure.Services;

namespace SeatHunter.Infrastructure.Tests.Services;

public class StripePaymentGatewayTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 17, 15, 0, 0, TimeSpan.Zero);

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : null;
            return handler(request);
        }
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ShouldPostStripeCheckoutSessionAndReturnRedirectUrl()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"id":"cs_test_12345","url":"https://checkout.stripe.com/c/pay/cs_test_12345"}""",
                Encoding.UTF8,
                "application/json")
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Billing:Stripe:SecretKey"] = "sk_test_dummy_123"
            })
            .Build();

        var gateway = new StripePaymentGateway(new HttpClient(handler), config);

        var session = await gateway.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest(
            UserId: "user-1",
            UserEmail: "user@example.com",
            TargetTier: UserTier.Pro,
            Cycle: BillingCycle.Monthly,
            Amount: 199m,
            Currency: "TRY",
            IdempotencyKey: "idem-stripe-1",
            SuccessRedirectUrl: "https://app.seathunter.io/billing?ok=1",
            CancelRedirectUrl: "https://app.seathunter.io/billing?cancel=1"));

        session.Provider.Should().Be("Stripe");
        session.ExternalSessionId.Should().Be("cs_test_12345");
        session.CheckoutUrl.Should().Be("https://checkout.stripe.com/c/pay/cs_test_12345");
        handler.LastRequest!.Headers.Authorization!.Parameter.Should().Be("sk_test_dummy_123");
        handler.LastRequest.Headers.GetValues("Idempotency-Key").Should().ContainSingle().Which.Should().Be("idem-stripe-1");
        var decodedBody = Uri.UnescapeDataString(handler.LastBody!);
        decodedBody.Should().Contain("unit_amount]=19900");
        decodedBody.Should().Contain("currency]=try");
    }

    [Fact]
    public void VerifyWebhookSignature_ShouldAcceptValidStripeV1SignatureAndRejectStaleOrTampered()
    {
        const string webhookSecret = "whsec_test_secret_123";
        const string payload = """{"type":"checkout.session.completed","data":{"object":{"id":"cs_test_1"}}}""";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Billing:Stripe:WebhookSecret"] = webhookSecret
            })
            .Build();

        var gateway = new StripePaymentGateway(new HttpClient(), config, new FakeTimeProvider(FixedNow));

        var validTs = FixedNow.ToUnixTimeSeconds();
        var validSig = StripePaymentGateway.ComputeStripeV1Signature($"{validTs}.{payload}", webhookSecret);
        gateway.VerifyWebhookSignature(payload, $"t={validTs},v1={validSig}").Should().BeTrue();

        // Tampered payload
        gateway.VerifyWebhookSignature(payload + " ", $"t={validTs},v1={validSig}").Should().BeFalse();

        // Expired timestamp (> 300s old)
        var staleTs = FixedNow.AddMinutes(-10).ToUnixTimeSeconds();
        var staleSig = StripePaymentGateway.ComputeStripeV1Signature($"{staleTs}.{payload}", webhookSecret);
        gateway.VerifyWebhookSignature(payload, $"t={staleTs},v1={staleSig}").Should().BeFalse();
    }
}
