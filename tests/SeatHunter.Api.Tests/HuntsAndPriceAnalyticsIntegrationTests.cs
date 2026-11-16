using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Api.Tests;

public class HuntsAndPriceAnalyticsIntegrationTests : IClassFixture<SeatHunterApiFactory>
{
    private readonly SeatHunterApiFactory _factory;

    public HuntsAndPriceAnalyticsIntegrationTests(SeatHunterApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnifiedAndLegacyHuntRoutes_ShouldCreateListCancelAndDeleteCleanly()
    {
        var email = $"hunts-{Guid.NewGuid():N}@example.com";
        var (client, auth) = await _factory.RegisterAndCreateClientAsync(email);
        var eventUrl = $"https://www.example.com/events/rock-{Guid.NewGuid():N}";

        var createRes = await client.PostAsJsonAsync("/api/hunts", new
        {
            userId = auth.UserId,
            url = eventUrl,
            targetDate = DateTime.UtcNow.AddDays(5),
            language = "tr",
            targetEventName = "Rock Festivali"
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var createdJson = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var huntId = createdJson.GetProperty("huntRequestId").GetGuid();
        huntId.Should().NotBeEmpty();

        var activeFromPrimary = await client.GetFromJsonAsync<JsonElement>("/api/hunts/active");
        activeFromPrimary.GetArrayLength().Should().Be(1);
        activeFromPrimary[0].GetProperty("id").GetGuid().Should().Be(huntId);

        var activeFromAlias = await client.GetFromJsonAsync<JsonElement>("/api/huntrequests/active");
        activeFromAlias.GetArrayLength().Should().Be(1);
        activeFromAlias[0].GetProperty("id").GetGuid().Should().Be(huntId);

        var cancelRes = await client.PostAsync($"/api/hunts/cancel/{huntId}", content: null);
        cancelRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await client.GetFromJsonAsync<JsonElement>("/api/hunts/history");
        history.GetArrayLength().Should().Be(1);
        history[0].GetProperty("status").GetString().Should().Be("Cancelled");

        var deleteRes = await client.DeleteAsync($"/api/huntrequests/{huntId}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PriceAnalytics_ShouldEnforceTierAndRouteOwnership()
    {
        var routeUrl = $"https://www.example.com/events/symphony-{Guid.NewGuid():N}";
        var encodedRoute = Uri.EscapeDataString(routeUrl);
        await _factory.SeedPriceSnapshotAsync(routeUrl, 950m, DateTime.UtcNow.AddHours(-2));
        await _factory.SeedPriceSnapshotAsync(routeUrl, 1250m, DateTime.UtcNow.AddHours(-1));

        // 1. Free user blocked even if tracking the event
        var (freeClient, freeAuth) = await _factory.RegisterAndCreateClientAsync(
            $"free-{Guid.NewGuid():N}@example.com",
            tier: UserTier.Free);

        var freeHunt = await freeClient.PostAsJsonAsync("/api/hunts", new
        {
            userId = freeAuth.UserId,
            url = routeUrl,
            targetDate = DateTime.UtcNow.AddDays(3),
            language = "tr"
        });
        freeHunt.EnsureSuccessStatusCode();

        var freeRes = await freeClient.GetAsync($"/api/price-analytics/{encodedRoute}");
        freeRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 2. Pro user without a hunt on this route cannot enumerate someone else's route
        var (proClient, proAuth) = await _factory.RegisterAndCreateClientAsync(
            $"pro-{Guid.NewGuid():N}@example.com",
            tier: UserTier.Pro);

        var untrackedProRes = await proClient.GetAsync($"/api/price-analytics/{encodedRoute}");
        untrackedProRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Once the Pro user creates a hunt for the route, summary and history succeed
        var proHunt = await proClient.PostAsJsonAsync("/api/hunts", new
        {
            userId = proAuth.UserId,
            url = routeUrl,
            targetDate = DateTime.UtcNow.AddDays(3),
            language = "tr"
        });
        proHunt.EnsureSuccessStatusCode();

        var trackedSummaryRes = await proClient.GetAsync($"/api/price-analytics/{encodedRoute}");
        trackedSummaryRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await trackedSummaryRes.Content.ReadFromJsonAsync<JsonElement>();
        summary.GetProperty("minPrice").GetDecimal().Should().Be(950m);
        summary.GetProperty("maxPrice").GetDecimal().Should().Be(1250m);

        var trackedHistoryRes = await proClient.GetAsync($"/api/price-analytics/{encodedRoute}/history?days=30");
        trackedHistoryRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await trackedHistoryRes.Content.ReadFromJsonAsync<JsonElement>();
        history.GetArrayLength().Should().Be(2);

        // 4. Admin can inspect any route regardless of own hunts
        var (adminClient, _) = await _factory.RegisterAndCreateClientAsync(
            $"admin-price-{Guid.NewGuid():N}@example.com",
            role: "Admin");

        var adminRes = await adminClient.GetAsync($"/api/price-analytics/{encodedRoute}");
        adminRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
