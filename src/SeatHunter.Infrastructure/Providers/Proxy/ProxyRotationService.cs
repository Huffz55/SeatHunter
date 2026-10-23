using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Infrastructure.Providers.Proxy;

public class ProxyRotationService : IProxyRotationService
{
    private readonly List<ProxyDefinition> _proxies = new();
    private readonly ConcurrentDictionary<string, ProxyHealth> _healthMap = new();
    private readonly ILogger<ProxyRotationService> _logger;
    private int _currentIndex = -1;

    private class ProxyHealth
    {
        public int ConsecutiveFailures { get; set; }
        public DateTime? CooldownUntil { get; set; }
    }

    public ProxyRotationService(IConfiguration configuration, ILogger<ProxyRotationService> logger)
    {
        _logger = logger;
        LoadProxies(configuration);
    }

    public bool HasProxies => _proxies.Count > 0;
    public int TotalProxies => _proxies.Count;
    public int CoolingDownProxiesCount
    {
        get
        {
            var now = DateTime.UtcNow;
            return _healthMap.Values.Count(h => h.CooldownUntil.HasValue && h.CooldownUntil.Value > now);
        }
    }

    public ProxyDefinition? GetNextProxy()
    {
        if (_proxies.Count == 0) return null;

        var now = DateTime.UtcNow;

        for (int i = 0; i < _proxies.Count; i++)
        {
            var index = (int)((uint)Interlocked.Increment(ref _currentIndex) % (uint)_proxies.Count);
            var candidate = _proxies[index];

            if (_healthMap.TryGetValue(candidate.Server, out var health))
            {
                if (health.CooldownUntil.HasValue && health.CooldownUntil.Value > now)
                {
                    continue; // Proxy geçici soğuma süresinde
                }
            }

            return candidate;
        }

        // Tüm proxyler soğumadaysa en azından birini dene
        var fallbackIndex = (int)((uint)Interlocked.Increment(ref _currentIndex) % (uint)_proxies.Count);
        return _proxies[fallbackIndex];
    }

    public void ReportFailure(string server)
    {
        var health = _healthMap.GetOrAdd(server, _ => new ProxyHealth());
        lock (health)
        {
            health.ConsecutiveFailures++;
            if (health.ConsecutiveFailures >= 3)
            {
                health.CooldownUntil = DateTime.UtcNow.AddMinutes(3);
                _logger.LogWarning("⚠️ Proxy {Server} 3 kez arka arkaya başarısız oldu. 3 dakika soğuma moduna alındı.", server);
            }
        }
    }

    public void ReportSuccess(string server)
    {
        if (_healthMap.TryGetValue(server, out var health))
        {
            lock (health)
            {
                health.ConsecutiveFailures = 0;
                health.CooldownUntil = null;
            }
        }
    }

    private void LoadProxies(IConfiguration configuration)
    {
        var proxySection = configuration.GetSection("Scraper:Proxies").Get<string[]>() 
                           ?? configuration["SCRAPER_PROXIES"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                           ?? Array.Empty<string>();

        foreach (var raw in proxySection)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            try
            {
                var uri = new Uri(raw.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? raw : $"http://{raw}");
                string server = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
                string? username = null;
                string? password = null;

                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var userParts = uri.UserInfo.Split(':');
                    username = userParts[0];
                    if (userParts.Length > 1) password = userParts[1];
                }

                _proxies.Add(new ProxyDefinition(server, username, password));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Geçersiz proxy formatı atlandı: {Raw}", raw);
            }
        }

        if (_proxies.Count > 0)
        {
            _logger.LogInformation("🚀 Toplam {Count} adet proxy havuzuna başarıyla yüklendi.", _proxies.Count);
        }
    }
}
