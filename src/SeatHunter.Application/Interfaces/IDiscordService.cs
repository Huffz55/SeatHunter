namespace SeatHunter.Application.Interfaces;

public interface IDiscordService
{
    Task SendTicketFoundAlertAsync(string eventName, string provider, string url, string? userEmail = null, CancellationToken cancellationToken = default);
}
