using FluentAssertions;
using SeatHunter.Application.Common;

namespace SeatHunter.Application.Tests;

public class NotificationChannelValidatorTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("https://discord.com/api/webhooks/1234567890/abcdef-token", true)]
    [InlineData("https://discordapp.com/api/webhooks/1234567890/abcdef-token", true)]
    [InlineData("https://canary.discord.com/api/webhooks/1234567890/abcdef-token", true)]
    [InlineData("http://discord.com/api/webhooks/1234567890/abcdef-token", false)]
    [InlineData("https://discord.com/channels/123/456", false)]
    [InlineData("https://169.254.169.254/api/webhooks/123/456", false)]
    [InlineData("https://evil.com/api/webhooks/123/456", false)]
    [InlineData("not-a-url", false)]
    public void IsValidDiscordWebhookUrl_ShouldEnforceHttpsAndDiscordWebhookPath(string? url, bool expected)
    {
        NotificationChannelValidator.IsValidDiscordWebhookUrl(url).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("2051304885", true)]
    [InlineData("-1001234567890", true)]
    [InlineData("@seathunter_channel", true)]
    [InlineData("0", false)]
    [InlineData("@ab", false)]
    [InlineData("invalid chat id with spaces", false)]
    public void IsValidTelegramChatId_ShouldValidateNumericAndChannelHandles(string? chatId, bool expected)
    {
        NotificationChannelValidator.IsValidTelegramChatId(chatId).Should().Be(expected);
    }
}
