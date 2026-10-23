using Microsoft.Playwright;

namespace SeatHunter.Infrastructure.Providers.Stealth;

public static class PlaywrightStealthManager
{
    private static readonly string[] RealisticUserAgents = new[]
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36"
    };

    public static string GetRandomUserAgent()
    {
        return RealisticUserAgents[Random.Shared.Next(RealisticUserAgents.Length)];
    }

    public static async Task ApplyStealthAsync(IBrowserContext context)
    {
        // 1. Anti-Bot ve Cloudflare parmak izi gizleme scriptleri (Stealth Injection)
        const string stealthScript = @"
            // navigator.webdriver kontrolünü tamamen gizle
            Object.defineProperty(navigator, 'webdriver', {
                get: () => undefined
            });

            // Normal bir Chrome tarayıcısı gibi window.chrome nesnesini taklit et
            window.chrome = {
                runtime: {},
                loadTimes: function() {},
                csi: function() {},
                app: {}
            };

            // Dilleri ve eklentileri gerçek kullanıcı profiline uygun şekilde tanımla
            Object.defineProperty(navigator, 'languages', {
                get: () => ['tr-TR', 'tr', 'en-US', 'en']
            });

            Object.defineProperty(navigator, 'plugins', {
                get: () => [1, 2, 3, 4, 5]
            });

            // Bildirim izinleri kontrolünü standartlaştır
            const originalQuery = window.navigator.permissions.query;
            window.navigator.permissions.query = (parameters) => (
                parameters.name === 'notifications' ?
                    Promise.resolve({ state: Notification.permission }) :
                    originalQuery(parameters)
            );
        ";

        await context.AddInitScriptAsync(stealthScript);
    }
}
