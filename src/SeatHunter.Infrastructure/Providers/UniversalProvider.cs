using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Constants;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Infrastructure.Providers.Captcha;
using SeatHunter.Infrastructure.Providers.Price;

namespace SeatHunter.Infrastructure.Providers;

/// <summary>
/// Tüm bilet sitelerini Playwright Stealth tarayıcısı üzerinden tek geçişte inceleyen evrensel sağlayıcı.
/// </summary>
public class UniversalProvider : ITicketProvider
{
    private static readonly string[] UniversalPositiveKeywords =
    [
        "bilet al",
        "buy",
        "comprar",
        "satın al",
        "add to cart",
        "sepete ekle",
        "ticket",
        "tickets",
        "ingressos",
        "disponível"
    ];

    private static readonly string[] UniversalNegativeKeywords =
    [
        "sold out",
        "tükendi",
        "esgotado",
        "unavailable"
    ];

    public string ProviderName => AppConstants.Providers.Universal;
    private readonly HttpClient _httpClient;
    private readonly IPlaywrightBrowserManager _browserManager;
    private readonly ICaptchaSolver _captchaSolver;
    private readonly ILogger<UniversalProvider> _logger;

    public UniversalProvider(
        HttpClient httpClient,
        IPlaywrightBrowserManager browserManager,
        ICaptchaSolver captchaSolver,
        ILogger<UniversalProvider> logger)
    {
        _httpClient = httpClient;
        _browserManager = browserManager;
        _captchaSolver = captchaSolver;
        _logger = logger;
    }

    public async Task<bool> CheckAvailabilityAsync(string url, CancellationToken cancellationToken)
    {
        var inspection = await InspectAvailabilityAsync(url, null, cancellationToken);
        return inspection.IsAvailable;
    }

    public async Task<TicketInspectionResult> InspectAvailabilityAsync(
        string url,
        string? preferredCategory = null,
        CancellationToken cancellationToken = default)
    {
        var html = await FetchRenderedHtmlAsync(url, cancellationToken);
        if (string.IsNullOrWhiteSpace(html))
        {
            return new TicketInspectionResult(false, null, string.IsNullOrWhiteSpace(preferredCategory));
        }

        return TicketHtmlEvaluator.Evaluate(
            html,
            UniversalPositiveKeywords,
            UniversalNegativeKeywords,
            preferredCategory,
            inspectScriptJson: true);
    }

    public async Task<string> ExtractEventNameAsync(string url, CancellationToken cancellationToken)
    {
        var html = await FetchRenderedHtmlAsync(url, cancellationToken);
        return string.IsNullOrWhiteSpace(html) ? string.Empty : TicketHtmlEvaluator.ExtractTitle(html);
    }

    public async Task<decimal?> ExtractPriceAsync(string url, CancellationToken cancellationToken)
    {
        var html = await FetchRenderedHtmlAsync(url, cancellationToken);
        return string.IsNullOrWhiteSpace(html) ? null : HtmlPriceExtractor.ExtractPrice(html);
    }

    private async Task<string?> FetchRenderedHtmlAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await _browserManager.CreateContextAsync();
            var page = await context.NewPageAsync();

            await page.RouteAsync("**/*", async route =>
            {
                if (route.Request.ResourceType is "image" or "stylesheet" or "font" or "media")
                {
                    await route.AbortAsync();
                }
                else
                {
                    await route.ContinueAsync();
                }
            });

            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 15000 });
            await Task.Delay(2000, cancellationToken);

            // Bot koruması & CAPTCHA (Turnstile / ReCaptcha) tespiti ve çözümü
            await PlaywrightCaptchaDetector.DetectAndSolveChallengeAsync(page, _captchaSolver, _logger, cancellationToken);

            return await page.ContentAsync();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "UniversalProvider sayfa içeriği okunamadı: {Url}", url);
            return null;
        }
    }
}
