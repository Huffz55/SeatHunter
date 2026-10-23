using Microsoft.Playwright;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Domain.Constants;
using SeatHunter.Infrastructure.Providers.Captcha;

namespace SeatHunter.Infrastructure.Providers;

public class UniversalProvider : ITicketProvider
{
    public string ProviderName => AppConstants.Providers.Universal;
    private readonly HttpClient _httpClient;
    private readonly IPlaywrightBrowserManager _browserManager;
    private readonly ICaptchaSolver _captchaSolver;
    private readonly ILogger<UniversalProvider> _logger;

    private static readonly Regex[] CompiledRegexes = new[]
    {
        new Regex("\"isSoldOut\"\\s*:\\s*false", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex("\"status\"\\s*:\\s*\"available\"", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex("\"availability\"\\s*:\\s*\"IN_STOCK\"", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex("\"availableSeats\"\\s*:\\s*\\[[^\\]]+\\]", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };

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
        try
        {
            await using var context = await _browserManager.CreateContextAsync();
            var page = await context.NewPageAsync();

            await page.RouteAsync("**/*", async route =>
            {
                if (route.Request.ResourceType == "image" ||
                    route.Request.ResourceType == "stylesheet" ||
                    route.Request.ResourceType == "font" ||
                    route.Request.ResourceType == "media")
                {
                    await route.AbortAsync();
                }
                else
                {
                    await route.ContinueAsync();
                }
            });

            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Task.Delay(2000, cancellationToken);
            
            // Bot koruması & CAPTCHA (Turnstile / ReCaptcha) tespiti ve çözümü
            await PlaywrightCaptchaDetector.DetectAndSolveChallengeAsync(page, _captchaSolver, _logger, cancellationToken);

            var html = await page.ContentAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 1. Script (JSON) Taraması (Deep Script Scanner)
            var scriptNodes = doc.DocumentNode.SelectNodes("//script");
            if (scriptNodes != null)
            {
                foreach (var script in scriptNodes)
                {
                    var scriptContent = script.InnerHtml;
                    if (string.IsNullOrWhiteSpace(scriptContent)) continue;
                    
                    if (CompiledRegexes.Any(r => r.IsMatch(scriptContent)))
                    {
                        return true;
                    }
                }
            }

            // 2. HTML Text Taraması
            var pageText = doc.DocumentNode.InnerText;
            var positiveKeywords = new[] { "bilet al", "buy", "comprar", "satın al", "add to cart", "sepete ekle", "ticket", "tickets", "ingressos", "disponível" };
            var negativeKeywords = new[] { "sold out", "tükendi", "esgotado", "unavailable" };

            bool hasNegative = negativeKeywords.Any(k => pageText.Contains(k, StringComparison.OrdinalIgnoreCase));
            bool hasPositive = positiveKeywords.Any(k => pageText.Contains(k, StringComparison.OrdinalIgnoreCase));

            return !hasNegative && hasPositive;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> ExtractEventNameAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await _browserManager.CreateContextAsync();
            var page = await context.NewPageAsync();

            await page.RouteAsync("**/*", async route =>
            {
                if (route.Request.ResourceType == "image" ||
                    route.Request.ResourceType == "stylesheet" ||
                    route.Request.ResourceType == "font" ||
                    route.Request.ResourceType == "media")
                {
                    await route.AbortAsync();
                }
                else
                {
                    await route.ContinueAsync();
                }
            });

            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Task.Delay(2000, cancellationToken);
            
            // Bot koruması & CAPTCHA (Turnstile / ReCaptcha) tespiti ve çözümü
            await PlaywrightCaptchaDetector.DetectAndSolveChallengeAsync(page, _captchaSolver, _logger, cancellationToken);

            var html = await page.ContentAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            
            var ogTitle = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")?.GetAttributeValue("content", "");
            if (!string.IsNullOrWhiteSpace(ogTitle)) return System.Net.WebUtility.HtmlDecode(ogTitle).Trim();
            
            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText;
            if (!string.IsNullOrWhiteSpace(title)) return System.Net.WebUtility.HtmlDecode(title).Trim();
        }
        catch { }
        return string.Empty;
    }
}
