using System.Globalization;
using System.Text.RegularExpressions;

namespace SeatHunter.Infrastructure.Providers.Price;

public static class HtmlPriceExtractor
{
    private static readonly Regex JsonPriceRegex = new(
        @"""(?:price|currentPrice|amount|lowestPrice|minPrice|lowPrice)""\s*:\s*""?([0-9]+(?:[.,][0-9]{1,2})?)""?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DataPriceRegex = new(
        @"data-(?:price|amount)=[""']([0-9]+(?:[.,][0-9]{1,2})?)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TurkishPriceRegex = new(
        @"([0-9]{1,3}(?:\.[0-9]{3})*(?:,[0-9]{1,2})?|[0-9]+(?:[.,][0-9]{1,2})?)\s*(?:TL|₺|TRY)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PrefixPriceRegex = new(
        @"(?:TL|₺|TRY)\s*([0-9]{1,3}(?:\.[0-9]{3})*(?:,[0-9]{1,2})?|[0-9]+(?:[.,][0-9]{1,2})?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static decimal? ExtractPrice(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var prices = new List<decimal>();

        // 1. JSON-LD / API script tag prices: "price": 3000, "price": "3000.00"
        var jsonMatches = JsonPriceRegex.Matches(content);
        foreach (Match match in jsonMatches)
        {
            if (match.Success && TryParseDecimal(match.Groups[1].Value, out var p) && p > 0)
            {
                prices.Add(p);
            }
        }

        // 2. data-price / data-amount attributes
        var dataMatches = DataPriceRegex.Matches(content);
        foreach (Match match in dataMatches)
        {
            if (match.Success && TryParseDecimal(match.Groups[1].Value, out var p) && p > 0)
            {
                prices.Add(p);
            }
        }

        // 3. Turkish text currency (suffix: 3.000,00₺, 3.000 TL, 750 TL)
        var trMatches = TurkishPriceRegex.Matches(content);
        foreach (Match match in trMatches)
        {
            if (match.Success && TryParseTurkishCurrency(match.Groups[1].Value, out var p) && p > 0)
            {
                prices.Add(p);
            }
        }

        // 4. Prefix currency (₺ 3.000, TL 500)
        var prefixMatches = PrefixPriceRegex.Matches(content);
        foreach (Match match in prefixMatches)
        {
            if (match.Success && TryParseTurkishCurrency(match.Groups[1].Value, out var p) && p > 0)
            {
                prices.Add(p);
            }
        }

        if (prices.Count > 0)
        {
            // En düşük bilet başlangıç fiyatını döndür
            return prices.Min();
        }

        return null;
    }

    private static bool TryParseDecimal(string raw, out decimal val)
    {
        raw = raw.Replace(',', '.');
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out val);
    }

    private static bool TryParseTurkishCurrency(string raw, out decimal val)
    {
        if (raw.Contains('.') && raw.Contains(','))
        {
            // 3.000,50 -> 3000.50
            raw = raw.Replace(".", "").Replace(',', '.');
        }
        else if (raw.Contains('.'))
        {
            var parts = raw.Split('.');
            if (parts.Length > 1 && parts[^1].Length == 3)
            {
                // 3.000 -> 3000
                raw = raw.Replace(".", "");
            }
        }
        else if (raw.Contains(','))
        {
            // 750,50 -> 750.50
            raw = raw.Replace(',', '.');
        }

        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out val);
    }
}
