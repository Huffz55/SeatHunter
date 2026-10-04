namespace SeatHunter.Application.Interfaces;

public interface ITelegramService
{
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);
}
