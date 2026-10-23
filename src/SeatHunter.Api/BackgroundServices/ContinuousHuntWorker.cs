using Microsoft.AspNetCore.SignalR;
using SeatHunter.Api.Hubs;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Enums;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Infrastructure.Persistence;
using SeatHunter.Domain.Constants;

namespace SeatHunter.Api.BackgroundServices;

public class ContinuousHuntWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ContinuousHuntWorker> _logger;
    private readonly IHuntManagerService _huntManager;
    private static readonly SemaphoreSlim _concurrencySemaphore = new(5, 5); // Maksimum 5 eşzamanlı kazıma işlemi

    public ContinuousHuntWorker(
        IServiceProvider serviceProvider, 
        ILogger<ContinuousHuntWorker> logger,
        IHuntManagerService huntManager)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _huntManager = huntManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Cold boot: Aktif taramaları Redis ZSET zamanlayıcısına senkronize et
        await SyncActiveHuntsToSchedulerAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SeatHunterDbContext>();
            var scheduler = scope.ServiceProvider.GetRequiredService<IDistributedHuntScheduler>();
            
            // 1. Önce yüksek performanslı Redis ZSET'ten zamanı gelen işleri atomik olarak çek
            var dueHuntIds = await scheduler.FetchDueHuntsAsync(DateTime.UtcNow, 50, stoppingToken);

            List<HuntRequest> dueRequests;
            if (dueHuntIds.Count > 0)
            {
                dueRequests = dbContext.HuntRequests
                    .Where(r => dueHuntIds.Contains(r.Id) && r.Status == RequestStatus.Active)
                    .ToList();
            }
            else
            {
                // Fallback: Redis boşsa veya bağlantı yoksa DB'den kontrol et
                dueRequests = dbContext.HuntRequests
                    .Where(r => r.Status == RequestStatus.Active && r.NextScanTime <= DateTime.UtcNow)
                    .Take(10)
                    .ToList();
            }

            var currentlyRunning = _huntManager.GetActiveHunts().Select(h => h.Id).ToList();

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
                    _huntManager.AddHunt(dto, cts);
                    
                    _ = Task.Run(() => ProcessSingleCheckAsync(request.Id, cts.Token), cts.Token);
                }
            }
            
            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task SyncActiveHuntsToSchedulerAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var scheduler = scope.ServiceProvider.GetRequiredService<IDistributedHuntScheduler>();
            var scheduledCount = await scheduler.GetScheduledCountAsync(stoppingToken);

            if (scheduledCount == 0)
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeatHunterDbContext>();
                var activeHunts = dbContext.HuntRequests
                    .Where(r => r.Status == RequestStatus.Active)
                    .ToList();

                if (activeHunts.Count > 0)
                {
                    _logger.LogInformation("🔄 Başlangıç senkronizasyonu: {Count} aktif av görevi Redis ZSET zamanlayıcısına yükleniyor...", activeHunts.Count);
                    foreach (var hunt in activeHunts)
                    {
                        await scheduler.ScheduleHuntAsync(hunt.Id, hunt.NextScanTime, stoppingToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis ZSET başlangıç senkronizasyonu sırasında hata oluştu. DB tabanlı fallback devrede.");
        }
    }

    private async Task ProcessSingleCheckAsync(Guid requestId, CancellationToken token)
    {
        try
        {
            await _concurrencySemaphore.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            _huntManager.RemoveHunt(requestId.ToString());
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeatHunterDbContext>();
        var ticketProviderFactory = scope.ServiceProvider.GetRequiredService<ITicketProviderFactory>();
        var notificationDispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        var scraperMetrics = scope.ServiceProvider.GetService<IScraperMetrics>();
        var scheduler = scope.ServiceProvider.GetRequiredService<IDistributedHuntScheduler>();
        
        HuntRequest? request = null;

        try
        {
            request = await dbContext.HuntRequests.FindAsync(new object[] { requestId }, token);
            if (request == null || request.Status != RequestStatus.Active) 
            {
                return;
            }
            
            var ticketProvider = ticketProviderFactory.GetProvider(request.RouteOrEventId);

            _logger.LogInformation("{Provider} üzerinden {Route} için tarama yapılıyor...", request.ProviderName, request.RouteOrEventId);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool isFound;
            try
            {
                isFound = await ticketProvider.CheckAvailabilityAsync(request.RouteOrEventId, token);
                sw.Stop();
                scraperMetrics?.RecordScrapeAttempt(request.ProviderName, isFound ? "found" : "not_found", sw.Elapsed.TotalMilliseconds);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                scraperMetrics?.RecordScrapeAttempt(request.ProviderName, "canceled", sw.Elapsed.TotalMilliseconds);
                throw;
            }
            catch (Exception)
            {
                sw.Stop();
                scraperMetrics?.RecordScrapeAttempt(request.ProviderName, "error", sw.Elapsed.TotalMilliseconds);
                throw;
            }
            
            if (isFound)
            {
                request.Status = RequestStatus.Completed;
                request.CompletedAt = DateTime.UtcNow;

                var user = await dbContext.Users.FindAsync(new object[] { request.UserId }, token);

                // VIP Auto-Reserve & Hold kontrolü
                bool isReserved = false;
                string? cartUrl = null;
                DateTime? reservedUntil = null;

                if (request.AutoReserveEnabled && user?.Tier == UserTier.Vip)
                {
                    var reservationEngine = scope.ServiceProvider.GetService<ITicketReservationEngine>();
                    if (reservationEngine != null)
                    {
                        _logger.LogInformation("⚡ VIP Otomatik Sepete Ekleme başlatılıyor - Request ID: {RequestId}", request.Id);
                        var reserveResult = await reservationEngine.AttemptReserveAsync(request, token);
                        if (reserveResult.IsSuccess)
                        {
                            isReserved = true;
                            cartUrl = reserveResult.CartUrl;
                            reservedUntil = reserveResult.ExpiresAt;
                            request.ReservationCartUrl = cartUrl;
                            request.ReservedUntil = reservedUntil;
                            _logger.LogInformation("✅ VIP Koltuk başarıyla rezerve edildi! Sepet linki: {CartUrl}", cartUrl);
                        }
                    }
                }

                await dbContext.SaveChangesAsync(token);
                await scheduler.RemoveHuntAsync(request.Id, token);

                var notification = new TicketFoundNotification(
                    RequestId: request.Id,
                    UserId: request.UserId,
                    UserEmail: user?.Email,
                    ProviderName: request.ProviderName,
                    RouteOrEventId: request.RouteOrEventId,
                    EventName: request.EventName,
                    Language: request.Language,
                    IsAutoReserved: isReserved,
                    ReservationCartUrl: cartUrl,
                    ReservedUntil: reservedUntil
                );

                await notificationDispatcher.DispatchTicketFoundAsync(notification, token);
            }
            else
            {
                // Etkinlik tarihi geçmişse taramayı sonlandır (Smart Expire)
                if (request.TargetDate < DateTime.UtcNow)
                {
                    request.Status = RequestStatus.Expired;
                    _logger.LogInformation("Etkinlik tarihi geçmiş olduğu için tarama sonlandırıldı (Expired) - Request ID: {RequestId}", request.Id);
                    await dbContext.SaveChangesAsync(token);
                    await scheduler.RemoveHuntAsync(request.Id, token);
                    return;
                }

                var user = await dbContext.Users.FindAsync(new object[] { request.UserId }, token);
                var userTier = user?.Tier ?? UserTier.Free;

                var timeUntilTarget = request.TargetDate - DateTime.UtcNow;
                if (timeUntilTarget.TotalDays > 30)
                {
                    request.NextScanTime = DateTime.UtcNow.AddHours(6);
                }
                else if (timeUntilTarget.TotalDays >= 7)
                {
                    request.NextScanTime = DateTime.UtcNow.AddHours(1);
                }
                else if (timeUntilTarget.TotalDays >= 1)
                {
                    request.NextScanTime = userTier switch
                    {
                        UserTier.Vip => DateTime.UtcNow.AddMinutes(2),
                        UserTier.Pro => DateTime.UtcNow.AddMinutes(5),
                        _ => DateTime.UtcNow.AddMinutes(15)
                    };
                }
                else
                {
                    // Son 24 saat: Plan tabanlı agresif tarama (Sniper modu)
                    request.NextScanTime = userTier switch
                    {
                        UserTier.Vip => DateTime.UtcNow.AddSeconds(15),
                        UserTier.Pro => DateTime.UtcNow.AddMinutes(1),
                        _ => DateTime.UtcNow.AddMinutes(5)
                    };
                }

                _logger.LogInformation("Bilet bulunamadı ({RequestId}) [Plan: {Tier}]. Sonraki tarama: {NextScanTime}", request.Id, userTier, request.NextScanTime.ToLocalTime());
                await dbContext.SaveChangesAsync(token);
                await scheduler.ScheduleHuntAsync(request.Id, request.NextScanTime, token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Tarama iptal edildi (Graceful Shutdown) - Request ID: {RequestId}", requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tarama sırasında kritik hata oluştu! Request ID: {RequestId}", requestId);
            if (request != null)
            {
                request.NextScanTime = DateTime.UtcNow.AddMinutes(5);
                await dbContext.SaveChangesAsync(CancellationToken.None);
                await scheduler.ScheduleHuntAsync(request.Id, request.NextScanTime, CancellationToken.None);
            }
        }
        finally
        {
            _concurrencySemaphore.Release();
            _huntManager.RemoveHunt(requestId.ToString());
        }
    }
}
