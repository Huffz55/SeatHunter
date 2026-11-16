namespace SeatHunter.Application.Common;

/// <summary>
/// Kullanıcı bildirim kanalı ayarları için SSRF ve format doğrulama yardımcısı.
/// </summary>
public static class NotificationChannelValidator
{
    private static readonly HashSet<string> AllowedDiscordHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "discord.com",
        "discordapp.com",
        "canary.discord.com",
        "ptb.discord.com"
    };

    public static bool IsValidDiscordWebhookUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps) return false;

        if (!AllowedDiscordHosts.Contains(uri.Host)) return false;

        return uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidTelegramChatId(string? chatId)
    {
        if (string.IsNullOrWhiteSpace(chatId)) return true;

        var trimmed = chatId.Trim();
        if (trimmed.Length > 64) return false;

        // Sayısal chat/kanal ID (örn. -100123456 veya 2051304885) ya da @kanalAdi
        if (long.TryParse(trimmed, out var numericId)) return numericId != 0;
        return trimmed.StartsWith('@') && trimmed.Length >= 4 && trimmed.Skip(1).All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}
