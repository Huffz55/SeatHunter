using Microsoft.Playwright;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using SeatHunter.Domain.Interfaces;

namespace SeatHunter.Infrastructure.Providers;

public class UniversalProvider : ITicketProvider
{
    public string ProviderName => "Universal";
    private readonly HttpClient _httpClient;

    public UniversalProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

                public async Task<bool> CheckAvailabilityAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

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
            
            var html = await page.ContentAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 1. Script (JSON) Taraması (Deep Script Scanner)
            var scriptNodes = doc.DocumentNode.SelectNodes("//script");
            if (scriptNodes != null)
            {
                var regexes = new[] 
                {
                    new Regex("\"isSoldOut\"\\s*:\\s*false", RegexOptions.IgnoreCase),
                    new Regex("\"status\"\\s*:\\s*\"available\"", RegexOptions.IgnoreCase),
                    new Regex("\"availability\"\\s*:\\s*\"IN_STOCK\"", RegexOptions.IgnoreCase),
                    new Regex("\"availableSeats\"\\s*:\\s*\\[[^\\]]+\\]", RegexOptions.IgnoreCase)
                };

                foreach (var script in scriptNodes)
                {
                    var scriptContent = script.InnerHtml;
                    if (string.IsNullOrWhiteSpace(scriptContent)) continue;
                    
                    if (regexes.Any(r => r.IsMatch(scriptContent)))
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
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

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
