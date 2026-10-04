using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Infrastructure.Services;

public class DummyEmailService : IEmailService
{
    private readonly ILogger<DummyEmailService> _logger;

    public DummyEmailService(ILogger<DummyEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendTicketFoundEmailAsync(string toEmail, string route, string url, string language)
    {
        _logger.LogInformation("================================================");
        _logger.LogInformation("EMAIL GÖNDERİLDİ -> Alıcı: {ToEmail}, Rota: {Route}, Mesaj: Biletiniz bulundu! Bilet Linki: {Url}", toEmail, route, url);
        _logger.LogInformation("================================================");
        
        return Task.CompletedTask;
    }
}
