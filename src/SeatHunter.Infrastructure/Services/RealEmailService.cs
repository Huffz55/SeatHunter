using System.Net;
using Microsoft.Extensions.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Infrastructure.Services;

public class RealEmailService : IEmailService
{
    private readonly ILogger<RealEmailService> _logger;
    private readonly string _emailFrom;
    private readonly string _emailPassword;
    private readonly string _smtpServer;
    private readonly int _smtpPort;

    public RealEmailService(ILogger<RealEmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _emailFrom = configuration["Email:From"] ?? "mehmethalimbas@gmail.com";
        _emailPassword = configuration["Email:Password"] ?? "INVALID_PASSWORD";
        _smtpServer = configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
        _smtpPort = int.TryParse(configuration["Email:SmtpPort"], out var port) ? port : 587;
    }

    public async Task SendTicketFoundEmailAsync(string toEmail, string route, string url, string language)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Seat Hunter", _emailFrom));
            message.To.Add(new MailboxAddress("", toEmail));

            bool isEn = language.ToLowerInvariant() == "en";
            message.Subject = isEn ? "Seat Hunter - Ticket Found!" : "Seat Hunter - Bilet Bulundu!";

            // HTML Enjeksiyon Koruması: Dış girdileri güvenli hale getir
            var safeRoute = WebUtility.HtmlEncode(route ?? string.Empty);
            var safeUrl = WebUtility.HtmlEncode(url ?? string.Empty);

            var title = isEn ? "🎉 Great News! Ticket Found!" : "🎉 Müjde! Bilet Bulundu!";
            var subtitle = isEn ? "<b>Route / Event ID:</b>" : "<b>Etkinlik / Rota ID:</b>";
            var text = isEn ? "Our system has caught an available seat for you. Click the button below to buy your ticket immediately:" : "Sistemimiz sizin için bir boş koltuk yakaladı. Hemen biletinizi almak için aşağıdaki bağlantıya tıklayın:";
            var button = isEn ? "Buy Ticket" : "Bileti Satın Al";
            var footer = isEn ? "Seat Hunter Automated Notification System" : "Seat Hunter Otomatik Bildirim Sistemi";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                    <div style='font-family: Arial, sans-serif; background-color: #f9f9f9; padding: 20px;'>
                        <div style='background-color: #ffffff; padding: 20px; border-radius: 8px; border: 1px solid #ddd;'>
                            <h2 style='color: #2c3e50;'>{title}</h2>
                            <p style='font-size: 16px; color: #333;'>{subtitle} <span style='color: #e74c3c;'>{safeRoute}</span></p>
                            <p style='font-size: 16px; color: #333;'>{text}</p>
                            <p style='text-align: center; margin-top: 30px;'>
                                <a href='{safeUrl}' style='background-color: #27ae60; color: #fff; padding: 12px 24px; text-decoration: none; border-radius: 4px; font-weight: bold;'>{button}</a>
                            </p>
                            <hr style='margin-top: 40px; border: 0; border-top: 1px solid #eee;'/>
                            <p style='color: #999; font-size: 12px; text-align: center;'>{footer}</p>
                        </div>
                    </div>"
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_emailFrom, _emailPassword);
            
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Gerçek EMAIL BAŞARIYLA GÖNDERİLDİ -> Alıcı: {ToEmail}, Rota: {Route}", toEmail, safeRoute);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gerçek mail gönderimi sırasında bir hata oluştu. Alıcı: {ToEmail}", toEmail);
        }
    }
}
