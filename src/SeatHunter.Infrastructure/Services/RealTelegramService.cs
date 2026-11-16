using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;
using Telegram.Bot;

namespace SeatHunter.Infrastructure.Services;

public class RealTelegramService : ITelegramService
{
    private readonly TelegramBotClient? _botClient;
    private readonly string _chatId;
    private readonly ILogger<RealTelegramService> _logger;

    public RealTelegramService(ILogger<RealTelegramService> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        var token = configuration["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Telegram:BotToken yapılandırılmadığı için Telegram bildirimleri pasif durumda.");
            _botClient = null;
        }
        else
        {
            try
            {
                _botClient = new TelegramBotClient(token);
                _logger.LogInformation("TelegramBotClient başarıyla başlatıldı.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telegram:BotToken geçersiz formatta olduğu için Telegram bildirimleri pasif durumda.");
                _botClient = null;
            }
        }

        _chatId = configuration["Telegram:ChatId"] ?? string.Empty;
    }

    public Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
        => SendMessageAsync(message, null, cancellationToken);

    public async Task SendMessageAsync(string message, string? targetChatId, CancellationToken cancellationToken = default)
    {
        var effectiveChatId = !string.IsNullOrWhiteSpace(targetChatId) ? targetChatId.Trim() : _chatId;
        if (_botClient == null || string.IsNullOrWhiteSpace(effectiveChatId))
        {
            _logger.LogWarning("Telegram bot istemcisi veya ChatId yapılandırılmadığı için mesaj iletilemedi: {Message}", message);
            return;
        }

        try
        {
            await _botClient.SendMessage(chatId: effectiveChatId, text: message, cancellationToken: cancellationToken);
            _logger.LogInformation("Telegram mesajı başarıyla gönderildi: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram mesajı gönderilirken hata oluştu!");
        }
    }
}
