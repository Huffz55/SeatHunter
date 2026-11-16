namespace SeatHunter.Domain.Interfaces;

/// <summary>
/// Tek bir sayfa taramasından elde edilen müsaitlik, kategori eşleşmesi ve fiyat bilgisini taşır.
/// </summary>
public sealed record TicketInspectionResult(
    bool IsAvailable,
    decimal? Price = null,
    bool CategoryMatched = true);

public interface ITicketProvider
{
    string ProviderName { get; }
    Task<bool> CheckAvailabilityAsync(string url, CancellationToken cancellationToken);
    Task<string> ExtractEventNameAsync(string url, CancellationToken cancellationToken);
    Task<decimal?> ExtractPriceAsync(string url, CancellationToken cancellationToken);

    /// <summary>
    /// Sayfayı tek seferde inceleyerek müsaitlik, kategori eşleşmesi ve fiyat bilgisini birlikte döndürür.
    /// </summary>
    async Task<TicketInspectionResult> InspectAvailabilityAsync(
        string url,
        string? preferredCategory = null,
        CancellationToken cancellationToken = default)
    {
        var isAvailable = await CheckAvailabilityAsync(url, cancellationToken);
        decimal? price = null;
        if (isAvailable)
        {
            price = await ExtractPriceAsync(url, cancellationToken);
        }

        return new TicketInspectionResult(isAvailable, price, CategoryMatched: true);
    }
}
