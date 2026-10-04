namespace SeatHunter.Domain.Interfaces;

public interface ITicketProviderFactory
{
    ITicketProvider GetProvider(string url);
}