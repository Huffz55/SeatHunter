namespace SeatHunter.Domain.Interfaces;
public interface ITicketProvider {
    string ProviderName { get; }
    Task<bool> CheckAvailabilityAsync(string url, CancellationToken cancellationToken);
    Task<string> ExtractEventNameAsync(string url, CancellationToken cancellationToken);
}
