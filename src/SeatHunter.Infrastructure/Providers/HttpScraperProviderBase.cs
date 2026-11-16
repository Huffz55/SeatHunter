using System.Net;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Infrastructure.Providers.Headers;
using SeatHunter.Infrastructure.Providers.Price;

namespace SeatHunter.Infrastructure.Providers;

/// <summary>
/// HTTP tabanlı bilet sağlayıcıları için ortak tek-geçişli tarama ve tarayıcı fallback tabanı.
/// </summary>
public abstract class HttpScraperProviderBase : ITicketProvider
{
    private readonly HttpClient _httpClient;
    private readonly ITicketProvider? _browserFallback;

    protected HttpScraperProviderBase(HttpClient httpClient, ITicketProvider? browserFallback = null)
    {
        _httpClient = httpClient;
        _browserFallback = browserFallback;
    }

    public abstract string ProviderName { get; }
    protected abstract IReadOnlyList<string> PositiveKeywords { get; }
    protected abstract IReadOnlyList<string> NegativeKeywords { get; }
    protected virtual bool InspectScriptJson => true;

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
        var (status, html, fallbackRequired) = await TryFetchHtmlAsync(url, cancellationToken);
        if (fallbackRequired && _browserFallback != null)
        {
            try
            {
                return await _browserFallback.InspectAvailabilityAsync(url, preferredCategory, cancellationToken);
            }
            catch
            {
                return new TicketInspectionResult(false, null, string.IsNullOrWhiteSpace(preferredCategory));
            }
        }

        if (fallbackRequired || status == null || html == null)
        {
            return new TicketInspectionResult(false, null, string.IsNullOrWhiteSpace(preferredCategory));
        }

        return TicketHtmlEvaluator.Evaluate(
            html,
            PositiveKeywords,
            NegativeKeywords,
            preferredCategory,
            InspectScriptJson);
    }

    public async Task<string> ExtractEventNameAsync(string url, CancellationToken cancellationToken)
    {
        var (_, html, fallbackRequired) = await TryFetchHtmlAsync(url, cancellationToken);
        if (fallbackRequired && _browserFallback != null)
        {
            try
            {
                return await _browserFallback.ExtractEventNameAsync(url, cancellationToken);
            }
            catch
            {
                return string.Empty;
            }
        }

        return html == null ? string.Empty : TicketHtmlEvaluator.ExtractTitle(html);
    }

    public async Task<decimal?> ExtractPriceAsync(string url, CancellationToken cancellationToken)
    {
        var (_, html, fallbackRequired) = await TryFetchHtmlAsync(url, cancellationToken);
        if (fallbackRequired && _browserFallback != null)
        {
            try
            {
                return await _browserFallback.ExtractPriceAsync(url, cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        return html == null ? null : HtmlPriceExtractor.ExtractPrice(html);
    }

    private async Task<(HttpStatusCode? StatusCode, string? Html, bool FallbackRequired)> TryFetchHtmlAsync(
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ScraperHeaderGenerator.ApplyBrowserHeaders(request);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            if (TicketHtmlEvaluator.RequiresBrowserRendering(response.StatusCode, html))
            {
                return (response.StatusCode, null, true);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (response.StatusCode, null, false);
            }

            return (response.StatusCode, html, false);
        }
        catch
        {
            return (null, null, true);
        }
    }
}
