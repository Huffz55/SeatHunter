namespace SeatHunter.Application.Interfaces;

public interface IEmailService
{
    Task SendTicketFoundEmailAsync(string toEmail, string route, string url, string language);
}
