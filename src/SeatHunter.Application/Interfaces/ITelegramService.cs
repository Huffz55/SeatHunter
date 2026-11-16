namespace SeatHunter.Application.Interfaces;

public interface ITelegramService
{
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);

    Task SendMessageAsync(string message, string? targetChatId, CancellationToken cancellationToken = default)
        => SendMessageAsync(message, cancellationToken);
}
