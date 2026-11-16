using SeatHunter.Application.Interfaces;

namespace SeatHunter.Api.BackgroundServices;

/// <summary>
/// Süresi dolan abonelikleri periyodik olarak tarayıp kullanıcıları otomatik Free plana düşürür.
/// </summary>
public class SubscriptionExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionExpirationWorker> _logger;

    public SubscriptionExpirationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SubscriptionExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var billingService = scope.ServiceProvider.GetRequiredService<IBillingService>();
                var expiredCount = await billingService.ExpireDueSubscriptionsAsync(stoppingToken);
                if (expiredCount > 0)
                {
                    _logger.LogInformation("{Count} adet süresi dolan abonelik Free plana düşürüldü.", expiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Abonelik süre kontrolü sırasında hata oluştu.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
