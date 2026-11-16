using SeatHunter.Domain.Constants;

namespace SeatHunter.Infrastructure.Providers;

/// <summary>
/// Obilet sayfalarını tek geçişte HTTP üzerinden tarar, bot koruması/JS engeli varsa tarayıcı fallback'e geçer.
/// </summary>
public class ObiletScraperProvider : HttpScraperProviderBase
{
    private static readonly string[] ObiletPositiveKeywords =
    [
        "Koltuk Seç",
        "Seferleri Gör",
        "Satın Al",
        "Bilet Al"
    ];

    private static readonly string[] ObiletNegativeKeywords =
    [
        "Tükendi",
        "Sefer Bulunamadı",
        "Dolu",
        "Aradığınız kriterlere uygun sefer",
        "Hata",
        "Bulunamadı"
    ];

    public ObiletScraperProvider(HttpClient httpClient, UniversalProvider? browserFallback = null)
        : base(httpClient, browserFallback)
    {
    }

    public override string ProviderName => AppConstants.Providers.ObiletLive;
    protected override IReadOnlyList<string> PositiveKeywords => ObiletPositiveKeywords;
    protected override IReadOnlyList<string> NegativeKeywords => ObiletNegativeKeywords;
    protected override bool InspectScriptJson => false;
}
