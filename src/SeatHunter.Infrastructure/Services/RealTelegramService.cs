using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;
using Telegram.Bot;

namespace SeatHunter.Infrastructure.Services;

public class RealTelegramService : ITelegramService
{
    private readonly TelegramBotClient _botClient;
    private readonly string _chatId = "2051304885";
    private readonly ILogger<RealTelegramService> _logger;

    public RealTelegramService(ILogger<RealTelegramService> logger, IConfiguration configuration)
    {
        _logger = logger;
        var token = configuration["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Telegram BotToken is missing in configuration!");
            token = "INVALID_TOKEN";
        }
        _botClient = new TelegramBotClient(token);
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
