using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Infrastructure.Providers.Price;

namespace SeatHunter.Infrastructure.Providers;

/// <summary>
/// Sağlayıcılar arası ortak HTML/JSON müsaitlik, kategori ve fiyat çözümleme yardımcısı.
/// </summary>
public static class TicketHtmlEvaluator
{
    private static readonly Regex[] JsonAvailabilityRegexes =
    [
        new Regex("\"isSoldOut\"\\s*:\\s*false", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex("\"status\"\\s*:\\s*\"(?:onsale|available)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex("\"availability\"\\s*:\\s*\"IN_STOCK\"", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex("\"availableSeats\"\\s*:\\s*\\[[^\\]]+\\]", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    ];

    private static readonly string[] CategoryNegativeMarkers =
    [
        "tükendi",
        "sold out",
        "satışa kapalı",
        "unavailable",
        "dolu",
        "esgotado",
        "\"isSoldOut\":true",
        "\"soldOut\":true",
        "\"available\":false",
        "\"availability\":\"OUT_OF_STOCK\""
    ];

    private static readonly string[] ChallengeMarkers =
    [
        "cf-browser-verification",
        "cf-turnstile",
        "checking your browser",
        "just a moment...",
        "attention required! | cloudflare",
        "please enable javascript"
    ];

    /// <summary>
    /// Verilen HTML içeriğini tek geçişte inceleyerek müsaitlik, kategori eşleşmesi ve fiyat sonucunu üretir.
    /// </summary>
    public static TicketInspectionResult Evaluate(
        string html,
        IReadOnlyList<string> positiveKeywords,
        IReadOnlyList<string> negativeKeywords,
        string? preferredCategory = null,
        bool inspectScriptJson = true)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new TicketInspectionResult(false, null, CategoryMatched: string.IsNullOrWhiteSpace(preferredCategory));
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        bool hasJsonPositive = inspectScriptJson && HasPositiveJsonSignal(doc, html);

        var pageText = doc.DocumentNode.InnerText ?? string.Empty;
        bool hasNegative = negativeKeywords.Any(k => pageText.Contains(k, StringComparison.OrdinalIgnoreCase));
        bool hasPositive = positiveKeywords.Any(k => pageText.Contains(k, StringComparison.OrdinalIgnoreCase));

        bool pageAvailable = hasJsonPositive || (!hasNegative && hasPositive);
        if (!pageAvailable)
        {
            return new TicketInspectionResult(false, null, CategoryMatched: string.IsNullOrWhiteSpace(preferredCategory));
        }

        if (string.IsNullOrWhiteSpace(preferredCategory))
        {
            return new TicketInspectionResult(true, HtmlPriceExtractor.ExtractPrice(html), CategoryMatched: true);
        }

        var (categoryMatched, categorySnippet) = EvaluateCategory(pageText, html, preferredCategory.Trim());
        if (!categoryMatched)
        {
            return new TicketInspectionResult(false, HtmlPriceExtractor.ExtractPrice(html), CategoryMatched: false);
        }

        var scopedPrice = categorySnippet != null ? HtmlPriceExtractor.ExtractPrice(categorySnippet) : null;
        return new TicketInspectionResult(true, scopedPrice ?? HtmlPriceExtractor.ExtractPrice(html), CategoryMatched: true);
    }

    /// <summary>
    /// HTML başlık meta etiketlerinden etkinlik adını ayıklar.
    /// </summary>
    public static string ExtractTitle(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var ogTitle = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")?.GetAttributeValue("content", "");
        if (!string.IsNullOrWhiteSpace(ogTitle)) return WebUtility.HtmlDecode(ogTitle).Trim();

        var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText;
        return string.IsNullOrWhiteSpace(title) ? string.Empty : WebUtility.HtmlDecode(title).Trim();
    }

    /// <summary>
    /// Yanıtın bot koruması veya boş SPA kabuğu nedeniyle Playwright render'ı gerektirip gerektirmediğini belirler.
    /// </summary>
    public static bool RequiresBrowserRendering(HttpStatusCode statusCode, string? html)
    {
        if (statusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            return true;
        }

        return ChallengeMarkers.Any(marker => html.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasPositiveJsonSignal(HtmlDocument doc, string rawHtml)
    {
        var scriptNodes = doc.DocumentNode.SelectNodes("//script");
        if (scriptNodes != null)
        {
            foreach (var script in scriptNodes)
            {
                var content = script.InnerHtml;
                if (!string.IsNullOrWhiteSpace(content) && JsonAvailabilityRegexes.Any(r => r.IsMatch(content)))
                {
                    return true;
                }
            }
        }

        return JsonAvailabilityRegexes.Any(r => r.IsMatch(rawHtml));
    }

    private static (bool Matched, string? BestSnippet) EvaluateCategory(string pageText, string rawHtml, string category)
    {
        var textSnippets = FindContextSnippets(pageText, category, maxLookback: 35, maxLookahead: 120, stopAtTags: false);
        var htmlSnippets = FindContextSnippets(rawHtml, category, maxLookback: 60, maxLookahead: 180, stopAtTags: true);

        var allSnippets = textSnippets.Count > 0 ? textSnippets : htmlSnippets;
        if (allSnippets.Count == 0)
        {
            return (false, null);
        }

        foreach (var snippet in allSnippets)
        {
            bool isSoldOutSnippet = CategoryNegativeMarkers.Any(m => snippet.Contains(m, StringComparison.OrdinalIgnoreCase));
            if (!isSoldOutSnippet)
            {
                return (true, snippet);
            }
        }

        return (false, null);
    }

    private static List<string> FindContextSnippets(
        string source,
        string needle,
        int maxLookback,
        int maxLookahead,
        bool stopAtTags)
    {
        var snippets = new List<string>();
        int startIndex = 0;

        while (startIndex < source.Length)
        {
            int idx = source.IndexOf(needle, startIndex, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) break;

            int left = Math.Max(0, idx - maxLookback);
            int right = Math.Min(source.Length, idx + needle.Length + maxLookahead);

            int prevNewline = source.LastIndexOf('\n', idx);
            if (prevNewline >= left) left = prevNewline + 1;

            int nextNewline = source.IndexOf('\n', idx + needle.Length);
            if (nextNewline >= 0 && nextNewline < right) right = nextNewline;

            if (stopAtTags)
            {
                int prevBrace = source.LastIndexOf('{', idx);
                if (prevBrace > left) left = prevBrace;

                int nextBrace = source.IndexOf('}', idx + needle.Length);
                if (nextBrace >= 0 && nextBrace < right) right = nextBrace + 1;
            }

            snippets.Add(source.Substring(left, right - left).Trim());

            startIndex = idx + needle.Length;
            if (snippets.Count >= 10) break;
        }

        return snippets;
    }
}
