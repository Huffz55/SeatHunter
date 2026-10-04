using Microsoft.AspNetCore.SignalR;
using SeatHunter.Api.Hubs;
using SeatHunter.Domain.Enums;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Infrastructure.Persistence;

namespace SeatHunter.Api.BackgroundServices;

public class ContinuousHuntWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<ContinuousHuntWorker> _logger;

    public ContinuousHuntWorker(IServiceProvider serviceProvider, IHubContext<NotificationHub> hubContext, ILogger<ContinuousHuntWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SeatHunterDbContext>();
            var huntManager = scope.ServiceProvider.GetRequiredService<SeatHunter.Application.Interfaces.IHuntManagerService>();
            
            var dueRequests = dbContext.HuntRequests
                .Where(r => r.Status == RequestStatus.Active && r.NextScanTime <= DateTime.UtcNow)
                .ToList();

            var currentlyRunning = huntManager.GetActiveHunts().Select(h => h.Id).ToList();

            foreach (var request in dueRequests)
            {
                if (!currentlyRunning.Contains(request.Id.ToString()))
                {
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    var dto = new SeatHunter.Application.DTOs.ActiveHuntDto
                    {
                        Id = request.Id.ToString(),
                        Provider = request.ProviderName,
                        EventId = request.RouteOrEventId,
                        Status = "Active",
                        StartTime = request.CreatedAt.ToLocalTime().ToString("HH:mm")
                    };
                    huntManager.AddHunt(dto, cts);
                    
                    _ = Task.Run(() => ProcessSingleCheckAsync(request.Id, cts.Token), cts.Token);
                }
            }
            
            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task ProcessSingleCheckAsync(Guid requestId, CancellationToken token)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeatHunterDbContext>();
        var huntManager = scope.ServiceProvider.GetRequiredService<SeatHunter.Application.Interfaces.IHuntManagerService>();
        var ticketProviderFactory = scope.ServiceProvider.GetRequiredService<ITicketProviderFactory>();
        var emailService = scope.ServiceProvider.GetRequiredService<SeatHunter.Application.Interfaces.IEmailService>();
        var telegramService = scope.ServiceProvider.GetRequiredService<SeatHunter.Application.Interfaces.ITelegramService>();
        
        var request = await dbContext.HuntRequests.FindAsync(new object[] { requestId }, token);
        if (request == null || request.Status != RequestStatus.Active) 
        {
            huntManager.RemoveHunt(requestId.ToString());
            return;
        }
        
        var ticketProvider = ticketProviderFactory.GetProvider(request.RouteOrEventId);

        try
        {
            _logger.LogInformation("{Provider} üzerinden {Route} için tarama yapılıyor...", request.ProviderName, request.RouteOrEventId);
            var isFound = await ticketProvider.CheckAvailabilityAsync(request.RouteOrEventId, token);
            
            if (isFound)
            {
                request.Status = RequestStatus.Completed;
                request.CompletedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(token);
                
                await _hubContext.Clients.All.SendAsync("SeatFound", new 
                { 
                    requestId = request.Id, 
                    message = $"{request.ProviderName} üzerinde {request.RouteOrEventId} rotası için boş koltuk yakalandı!" 
                }, token);

                var ticketUrl = request.RouteOrEventId;
                    
                var user = await dbContext.Users.FindAsync(new object[] { request.UserId }, token);
                var email = user?.Email ?? "mehmethalimbas@gmail.com";
                    
                await emailService.SendTicketFoundEmailAsync(email, request.RouteOrEventId, ticketUrl, request.Language);
                
                var telegramMessage = $"🚀 Sistem Raporu: {email} kullanıcısı için {request.ProviderName} üzerinde {request.RouteOrEventId} bileti başarıyla bulundu!";
                await telegramService.SendMessageAsync(telegramMessage, token);
            }
            else
            {
                var timeUntilTarget = request.TargetDate - DateTime.UtcNow;
                if (timeUntilTarget.TotalDays > 30)
                    request.NextScanTime = DateTime.UtcNow.AddHours(6);
                else if (timeUntilTarget.TotalDays >= 7)
                    request.NextScanTime = DateTime.UtcNow.AddHours(1);
                else if (timeUntilTarget.TotalDays >= 1)
                    request.NextScanTime = DateTime.UtcNow.AddMinutes(15);
                else
                    request.NextScanTime = DateTime.UtcNow.AddMinutes(1);

                _logger.LogInformation("Bilet bulunamadı ({RequestId}). Sonraki tarama: {NextScanTime}", request.Id, request.NextScanTime.ToLocalTime());
                await dbContext.SaveChangesAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Tarama iptal edildi (Graceful Shutdown) - Request ID: {RequestId}", requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tarama sırasında kritik hata oluştu! Request ID: {RequestId}", requestId);
            // Hata durumunda 5 dakika sonra tekrar dene
            request.NextScanTime = DateTime.UtcNow.AddMinutes(5);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            huntManager.RemoveHunt(requestId.ToString());
        }
    }
}
