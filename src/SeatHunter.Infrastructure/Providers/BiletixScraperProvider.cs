using SeatHunter.Domain.Constants;

namespace SeatHunter.Infrastructure.Providers;

/// <summary>
/// Biletix sayfalarını tek geçişte HTTP üzerinden tarar, bot koruması/JS engeli varsa tarayıcı fallback'e geçer.
/// </summary>
public class BiletixScraperProvider : HttpScraperProviderBase
{
    private static readonly string[] BiletixPositiveKeywords =
    [
        "Bilet Al",
        "Bilet Bul",
        "Kategori",
        "Genel Satış",
        "Sepete Ekle"
    ];

    private static readonly string[] BiletixNegativeKeywords =
    [
        "Tükendi",
        "Satışa Kapalı",
        "Yakında",
        "Aradığınız sayfa bulunamadı",
        "aradığınız kriterlere uygun",
        "Hata 404",
        "Aradığınız etkinlik bulunmamaktadır"
    ];

    public BiletixScraperProvider(HttpClient httpClient, UniversalProvider? browserFallback = null)
        : base(httpClient, browserFallback)
    {
    }

    public override string ProviderName => AppConstants.Providers.BiletixLive;
    protected override IReadOnlyList<string> PositiveKeywords => BiletixPositiveKeywords;
    protected override IReadOnlyList<string> NegativeKeywords => BiletixNegativeKeywords;
}
