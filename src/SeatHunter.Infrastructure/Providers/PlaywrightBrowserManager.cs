using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using SeatHunter.Application.Interfaces;
using SeatHunter.Infrastructure.Providers.Stealth;

namespace SeatHunter.Infrastructure.Providers;

public class PlaywrightBrowserManager : IPlaywrightBrowserManager
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly IProxyRotationService _proxyService;
    private readonly ILogger<PlaywrightBrowserManager> _logger;

    public PlaywrightBrowserManager(IProxyRotationService proxyService, ILogger<PlaywrightBrowserManager> logger)
    {
        _proxyService = proxyService;
        _logger = logger;
    }

    public async Task<IBrowserContext> CreateContextAsync()
    {
        if (_browser == null || !_browser.IsConnected)
        {
            await _initLock.WaitAsync();
            try
            {
                if (_browser == null || !_browser.IsConnected)
                {
                    _logger.LogInformation("Playwright Singleton Stealth Browser başlatılıyor...");
                    _playwright = await Playwright.CreateAsync();
                    _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = true,
                        Args = new[]
                        {
                            "--no-sandbox",
                            "--disable-setuid-sandbox",
                            "--disable-dev-shm-usage",
                            "--disable-gpu",
                            "--disable-blink-features=AutomationControlled" // Anti-Bot flag gizleme
                        }
                    });
                    _logger.LogInformation("Playwright Singleton Stealth Browser başarıyla hazırlandı.");
                }
            }
            finally
            {
                _initLock.Release();
            }
        }

        var contextOptions = new BrowserNewContextOptions
        {
            UserAgent = PlaywrightStealthManager.GetRandomUserAgent(),
            Locale = "tr-TR",
            TimezoneId = "Europe/Istanbul",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        };

        var proxy = _proxyService.GetNextProxy();
        if (proxy != null)
        {
            contextOptions.Proxy = new Microsoft.Playwright.Proxy
            {
                Server = proxy.Server,
                Username = proxy.Username,
                Password = proxy.Password
            };
            _logger.LogDebug("Playwright Context için proxy tanımlandı: {Server}", proxy.Server);
        }

        var context = await _browser.NewContextAsync(contextOptions);

        // Anti-bot script enjeksiyonu
        await PlaywrightStealthManager.ApplyStealthAsync(context);

        return context;
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.DisposeAsync();
        }
        _playwright?.Dispose();
    }
}
