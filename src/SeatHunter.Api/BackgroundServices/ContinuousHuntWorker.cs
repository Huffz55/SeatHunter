using Microsoft.AspNetCore.SignalR;
using SeatHunter.Api.Hubs;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Enums;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Infrastructure.Persistence;
using SeatHunter.Infrastructure.Providers;
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
        var executionService = scope.ServiceProvider.GetRequiredService<IHuntExecutionService>();

        try
        {
            await executionService.ExecuteHuntCheckAsync(requestId, rescheduleIfNotFound: true, token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Tarama iptal edildi (Graceful Shutdown) - Request ID: {RequestId}", requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tarama sırasında kritik hata oluştu! Request ID: {RequestId}", requestId);
        }
        finally
        {
            _concurrencySemaphore.Release();
            _huntManager.RemoveHunt(requestId.ToString());
        }
    }
}
