using Microsoft.Playwright;

namespace SeatHunter.Infrastructure.Providers;

public interface IPlaywrightBrowserManager : IAsyncDisposable
{
    Task<IBrowserContext> CreateContextAsync();
}
