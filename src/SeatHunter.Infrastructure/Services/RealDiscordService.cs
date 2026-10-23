using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Infrastructure.Services;

public class RealDiscordService : IDiscordService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RealDiscordService> _logger;

    public RealDiscordService(HttpClient httpClient, IConfiguration configuration, ILogger<RealDiscordService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendTicketFoundAlertAsync(string eventName, string provider, string url, string? userEmail = null, CancellationToken cancellationToken = default)
    {
        var webhookUrl = _configuration["Discord:WebhookUrl"] 
                         ?? _configuration["DISCORD_WEBHOOK_URL"];

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            _logger.LogDebug("Discord Webhook URL yapılandırılmamış, bildirim gönderilmedi.");
            return;
        }

        try
        {
            var payload = new
            {
                username = "SeatHunter Sniper",
                embeds = new[]
                {
                    new
                    {
                        title = "🎯 BOŞ KOLTUK BULUNDU!",
                        description = $"**{eventName}** için beklenen bilet veya boş koltuk yakalandı!",
                        url = url,
                        color = 3066993, // Zümrüt Yeşili
                        fields = new[]
                        {
                            new { name = "Sağlayıcı", value = provider, @inline = true },
                            new { name = "Hedef Kullanıcı", value = userEmail ?? "Bilinmiyor", @inline = true },
                            new { name = "Bilet Bağlantısı", value = $"[Doğrudan Satın Alma Sayfasına Git]({url})", @inline = false }
                        },
                        footer = new { text = "SeatHunter Real-Time Alert Engine" },
                        timestamp = DateTime.UtcNow.ToString("o")
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(webhookUrl, content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Discord webhook bildirimi başarıyla gönderildi: {EventName}", eventName);
            }
            else
            {
                _logger.LogWarning("Discord webhook gönderimi başarısız oldu. Durum Kodu: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discord webhook bildirimi gönderilirken hata oluştu.");
        }
    }
}
