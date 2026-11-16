using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SeatHunter.Infrastructure.Services;

namespace SeatHunter.Api.Tests;

public class BillingIntegrationTests : IClassFixture<SeatHunterApiFactory>
{
    private readonly SeatHunterApiFactory _factory;

    public BillingIntegrationTests(SeatHunterApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetPlans_ShouldReturnCatalogWithFreeProAndVip()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/billing/plans");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var plans = await res.Content.ReadFromJsonAsync<JsonElement>();
        plans.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task WebhookCheckoutFlow_ShouldVerifyHmacSignature_UpgradeTier_AndIgnoreReplays()
    {
        var email = $"webhook-{Guid.NewGuid():N}@example.com";
        var (client, _) = await _factory.RegisterAndCreateClientAsync(email);

        var checkoutRes = await client.PostAsJsonAsync("/api/billing/checkout", new
        {
            targetTier = "Pro",
            billingCycle = "Monthly",
            idempotencyKey = $"idem-{Guid.NewGuid():N}"
        });
        checkoutRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await checkoutRes.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = session.GetProperty("externalSessionId").GetString()!;

        var webhookPayload = JsonSerializer.Serialize(new
        {
            eventType = "payment.succeeded",
            externalSessionId = sessionId,
            externalSubscriptionId = "sub_webhook_test"
        });

        var anonClient = _factory.CreateClient();

        // 1. Geçersiz imza 401 dönmeli
        using var badRequest = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhook")
        {
            Content = new StringContent(webhookPayload, Encoding.UTF8, "application/json")
        };
        badRequest.Headers.Add("X-Billing-Signature", "sha256=invalidsignature");
        var badRes = await anonClient.SendAsync(badRequest);
        badRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Geçerli HMAC imza Pro plana yükseltmeli
        var validSig = SimulatedPaymentGateway.ComputeSignature(webhookPayload, SeatHunterApiFactory.TestWebhookSecret);
        using var validRequest = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhook")
        {
            Content = new StringContent(webhookPayload, Encoding.UTF8, "application/json")
        };
        validRequest.Headers.Add("X-Billing-Signature", $"sha256={validSig}");
        var goodRes = await anonClient.SendAsync(validRequest);
        goodRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var billingMe = await client.GetFromJsonAsync<JsonElement>("/api/billing/me");
        billingMe.GetProperty("effectiveTier").GetString().Should().Be("Pro");
        billingMe.GetProperty("transactions").GetArrayLength().Should().Be(1);
        billingMe.GetProperty("transactions")[0].GetProperty("status").GetString().Should().Be("Succeeded");

        // 3. Aynı webhook tekrarı idempotent dönmeli
        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhook")
        {
            Content = new StringContent(webhookPayload, Encoding.UTF8, "application/json")
        };
        replayRequest.Headers.Add("X-Billing-Signature", validSig);
        var replayRes = await anonClient.SendAsync(replayRequest);
        replayRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SandboxCheckoutConfirm_ShouldUpgradeUserUnlockPriceAnalyticsAndSupportCancelAutoRenew()
    {
        var email = $"sandbox-upgrade-{Guid.NewGuid():N}@example.com";
        var (client, auth) = await _factory.RegisterAndCreateClientAsync(email);

        var routeUrl = $"https://www.example.com/events/billing-{Guid.NewGuid():N}";
        await _factory.SeedPriceSnapshotAsync(routeUrl, 500m);

        var huntRes = await client.PostAsJsonAsync("/api/hunts", new
        {
            userId = auth.UserId,
            url = routeUrl,
            targetDate = DateTime.UtcNow.AddDays(4),
            language = "tr"
        });
        huntRes.EnsureSuccessStatusCode();

        // Free iken fiyat analitiği yasak
        var priceBefore = await client.GetAsync($"/api/price-analytics/{Uri.EscapeDataString(routeUrl)}");
        priceBefore.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Pro'ya yükselt ve sandbox onayı çalıştır
        var checkoutRes = await client.PostAsJsonAsync("/api/billing/checkout", new
        {
            targetTier = "Pro",
            billingCycle = "Yearly"
        });
        checkoutRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await checkoutRes.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = session.GetProperty("externalSessionId").GetString()!;

        var confirmRes = await client.PostAsync($"/api/billing/sandbox/confirm/{sessionId}", content: null);
        confirmRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmJson = await confirmRes.Content.ReadFromJsonAsync<JsonElement>();
        confirmJson.GetProperty("effectiveTier").GetString().Should().Be("Pro");
        var refreshedToken = confirmJson.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken);

        // Yükseltme sonrası kendi rotasında fiyat analitiği anında açılmalı
        var priceAfter = await client.GetAsync($"/api/price-analytics/{Uri.EscapeDataString(routeUrl)}");
        priceAfter.StatusCode.Should().Be(HttpStatusCode.OK);

        // Otomatik yenileme iptali dönem sonunu koruyarak CancelAtPeriodEnd = true yapmalı
        var cancelRes = await client.PostAsync("/api/billing/cancel", content: null);
        cancelRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var statusAfterCancel = await client.GetFromJsonAsync<JsonElement>("/api/billing/me");
        statusAfterCancel.GetProperty("effectiveTier").GetString().Should().Be("Pro");
        statusAfterCancel.GetProperty("activeSubscription").GetProperty("cancelAtPeriodEnd").GetBoolean().Should().BeTrue();
    }
}
