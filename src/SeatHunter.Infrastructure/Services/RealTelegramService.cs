using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;
using Telegram.Bot;

namespace SeatHunter.Infrastructure.Services;

public class RealTelegramService : ITelegramService
{
    private readonly TelegramBotClient _botClient;
    private readonly string _chatId;
    private readonly ILogger<RealTelegramService> _logger;

    public RealTelegramService(ILogger<RealTelegramService> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        var token = configuration["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Telegram:BotToken konfigürasyonda bulunamadı!");
            token = "INVALID_TOKEN";
        }
        _botClient = new TelegramBotClient(token);

        _chatId = configuration["Telegram:ChatId"] ?? "2051304885";
        if (string.IsNullOrWhiteSpace(configuration["Telegram:ChatId"]))
        {
            _logger.LogInformation("Telegram:ChatId konfigürasyonda belirtilmediği için varsayılan id kullanılıyor.");
        }
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _botClient.SendMessage(chatId: _chatId, text: message, cancellationToken: cancellationToken);
            _logger.LogInformation("Telegram mesajı başarıyla gönderildi: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram mesajı gönderilirken hata oluştu!");
        }
    }
}
