using HtmlAgilityPack;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Domain.Constants;

namespace SeatHunter.Infrastructure.Providers;

public class BiletixScraperProvider : ITicketProvider
{
    public string ProviderName => AppConstants.Providers.BiletixLive;
    private readonly HttpClient _httpClient;

    public BiletixScraperProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

        public async Task<bool> CheckAvailabilityAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            Headers.ScraperHeaderGenerator.ApplyBrowserHeaders(request);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return false;

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Dinamik JSON verilerinden biletin açık olup olmadığını kontrol et
            if (html.Contains("\"isSoldOut\":false", StringComparison.OrdinalIgnoreCase) || 
                html.Contains("\"status\":\"onsale\"", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("\"availability\":\"IN_STOCK\"", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("\"status\":\"AVAILABLE\"", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var pageText = doc.DocumentNode.InnerText;
            
            var negativeKeywords = new[] { "Tükendi", "Satışa Kapalı", "Yakında", "Aradığınız sayfa bulunamadı", "aradığınız kriterlere uygun", "Hata 404", "Aradığınız etkinlik bulunmamaktadır" };
            var positiveKeywords = new[] { "Bilet Al", "Bilet Bul", "Kategori", "Genel Satış", "Sepete Ekle" };

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
            Headers.ScraperHeaderGenerator.ApplyBrowserHeaders(request);

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
