using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Infrastructure.HealthChecks;

public class ProxyPoolHealthCheck : IHealthCheck
{
    private readonly IProxyRotationService _proxyService;

    public ProxyPoolHealthCheck(IProxyRotationService proxyService)
    {
        _proxyService = proxyService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var total = _proxyService.TotalProxies;
        var cooling = _proxyService.CoolingDownProxiesCount;
        var active = total - cooling;

        var data = new Dictionary<string, object>
        {
            { "TotalProxies", total },
            { "ActiveProxies", active },
            { "CoolingDownProxies", cooling }
        };

        if (total == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Doğrudan bağlantı modu aktif (Tanımlı proxy havuzu bulunmuyor).", data));
        }

        if (active == 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Tüm proxy'ler ({total}/{total}) soğuma modunda!", null, data));
        }

        if (cooling > 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                $"Proxy havuzu aktif: {active}/{total} çalışıyor ({cooling} soğumada).", data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Tüm proxy'ler aktif ve sağlıklı ({total}/{total}).", data));
    }
}
