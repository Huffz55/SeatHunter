using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace SeatHunter.Api.Tests;

public class AuthAndUserIntegrationTests : IClassFixture<SeatHunterApiFactory>
{
    private readonly SeatHunterApiFactory _factory;

    public AuthAndUserIntegrationTests(SeatHunterApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterLoginAndNotificationPreferences_ShouldPersistRoundTrip()
    {
        var email = $"notif-{Guid.NewGuid():N}@example.com";
        var (client, _) = await _factory.RegisterAndCreateClientAsync(email);

        var meInitial = await client.GetFromJsonAsync<JsonElement>("/api/user/me");
        meInitial.GetProperty("email").GetString().Should().Be(email);
        meInitial.GetProperty("notifyEmail").GetBoolean().Should().BeTrue();
        meInitial.GetProperty("notifyTelegram").GetBoolean().Should().BeTrue();
        meInitial.GetProperty("notifyDiscord").GetBoolean().Should().BeTrue();

        var customWebhook = "https://discord.com/api/webhooks/111222333444/secret-token-xyz";
        var updateRes = await client.PutAsJsonAsync("/api/user/notifications", new
        {
            notifyEmail = false,
            notifyTelegram = true,
            notifyDiscord = true,
            telegramChatId = "987654321",
            discordWebhookUrl = customWebhook
        });

        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var meUpdated = await client.GetFromJsonAsync<JsonElement>("/api/user/me");
        meUpdated.GetProperty("notifyEmail").GetBoolean().Should().BeFalse();
        meUpdated.GetProperty("notifyTelegram").GetBoolean().Should().BeTrue();
        meUpdated.GetProperty("notifyDiscord").GetBoolean().Should().BeTrue();
        meUpdated.GetProperty("telegramChatId").GetString().Should().Be("987654321");
        meUpdated.GetProperty("discordWebhookUrl").GetString().Should().Be(customWebhook);
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("https://evil.example.com/api/webhooks/123/abc")]
    [InlineData("http://discord.com/api/webhooks/123/abc")]
    public async Task UpdateNotificationPreferences_ShouldRejectNonDiscordWebhookUrls(string maliciousWebhook)
    {
        var email = $"ssrf-{Guid.NewGuid():N}@example.com";
        var (client, _) = await _factory.RegisterAndCreateClientAsync(email);

        var res = await client.PutAsJsonAsync("/api/user/notifications", new
        {
            notifyEmail = true,
            notifyTelegram = true,
            notifyDiscord = true,
            telegramChatId = (string?)null,
            discordWebhookUrl = maliciousWebhook
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SuspendedUser_ShouldReceive401Unauthorized_EvenWithValidJwt()
    {
        var email = $"suspended-{Guid.NewGuid():N}@example.com";
        var (client, _) = await _factory.RegisterAndCreateClientAsync(email);

        var initialRes = await client.GetAsync("/api/user/me");
        initialRes.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.UpdateUserInDatabaseAsync(email, u => u.IsActive = false);

        var revokedRes = await client.GetAsync("/api/user/me");
        revokedRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
