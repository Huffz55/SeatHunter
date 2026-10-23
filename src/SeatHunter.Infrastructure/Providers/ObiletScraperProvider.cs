using HtmlAgilityPack;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Domain.Constants;
using SeatHunter.Infrastructure.Providers.Headers;

namespace SeatHunter.Infrastructure.Providers;

public class ObiletScraperProvider : ITicketProvider
{
    public string ProviderName => AppConstants.Providers.ObiletLive;
    private readonly HttpClient _httpClient;

    public ObiletScraperProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> CheckAvailabilityAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            ScraperHeaderGenerator.ApplyBrowserHeaders(request);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return false;

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var pageText = doc.DocumentNode.InnerText;
            
            var negativeKeywords = new[] { "Tükendi", "Sefer Bulunamadı", "Dolu", "Aradığınız kriterlere uygun sefer", "Hata", "Bulunamadı" };
            var positiveKeywords = new[] { "Koltuk Seç", "Seferleri Gör", "Satın Al", "Bilet Al" };

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
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            ScraperHeaderGenerator.ApplyBrowserHeaders(request);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return string.Empty;
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
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
