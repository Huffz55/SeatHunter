namespace SeatHunter.Application.Interfaces;

public interface ISignalRNotifier
{
    Task SendSeatFoundAsync(string userId, Guid requestId, string message, CancellationToken cancellationToken = default);
}
