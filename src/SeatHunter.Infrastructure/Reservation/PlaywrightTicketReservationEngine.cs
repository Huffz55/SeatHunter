using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Infrastructure.Providers;
using SeatHunter.Infrastructure.Providers.Captcha;

namespace SeatHunter.Infrastructure.Reservation;

/// <summary>
/// Playwright ile hedef sayfada kategori seçimi ve sepete ekleme işlemini dener.
/// </summary>
public class PlaywrightTicketReservationEngine : ITicketReservationEngine
{
    private readonly IPlaywrightBrowserManager _browserManager;
    private readonly ICaptchaSolver _captchaSolver;
    private readonly MockTicketReservationEngine _fallbackEngine;
    private readonly ILogger<PlaywrightTicketReservationEngine> _logger;
    private readonly bool _allowMockFallback;

    public PlaywrightTicketReservationEngine(
        IPlaywrightBrowserManager browserManager,
        ICaptchaSolver captchaSolver,
        MockTicketReservationEngine fallbackEngine,
        ILogger<PlaywrightTicketReservationEngine> logger,
        IConfiguration? configuration = null)
    {
        _browserManager = browserManager;
        _captchaSolver = captchaSolver;
        _fallbackEngine = fallbackEngine;
        _logger = logger;
        _allowMockFallback = bool.TryParse(configuration?["Reservation:AllowMockFallback"], out var allow) && allow;
    }

    public async Task<ReservationResult> AttemptReserveAsync(HuntRequest hunt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🚀 [PlaywrightTicketReservationEngine] VIP Koltuk Hold & Sepetleme süreci başlatıldı: {Url}", hunt.RouteOrEventId);

        try
        {
            await using var context = await _browserManager.CreateContextAsync();
            var page = await context.NewPageAsync();

            await page.RouteAsync("**/*", async route =>
            {
                if (route.Request.ResourceType is "image" or "font" or "media")
                {
                    await route.AbortAsync();
                }
                else
                {
                    await route.ContinueAsync();
                }
            });

            await page.GotoAsync(hunt.RouteOrEventId, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 15000 });
            await Task.Delay(1500, cancellationToken);

            // 1. CAPTCHA & Cloudflare Turnstile bot kalkanını aş
            await PlaywrightCaptchaDetector.DetectAndSolveChallengeAsync(page, _captchaSolver, _logger, cancellationToken);

            // 2. Tercih edilen kategori/tribün belirtilmişse önce eşleşen seçeneği seçmeyi dene
            if (!string.IsNullOrWhiteSpace(hunt.PreferredCategory))
            {
                await TrySelectPreferredCategoryAsync(page, hunt.PreferredCategory.Trim());
            }

            // 3. Sepete Ekle / Bilet Al butonlarını tara
            var cartButtonSelectors = new[]
            {
                "button:has-text('Sepete Ekle')",
                "button:has-text('Add to Cart')",
                "button:has-text('Bilet Al')",
                "button:has-text('Buy Tickets')",
                "a:has-text('Sepete Ekle')",
                "a:has-text('Bilet Al')",
                ".btn-add-to-cart",
                ".buy-button",
                "#add-to-cart-button"
            };

            IElementHandle? targetButton = null;
            foreach (var selector in cartButtonSelectors)
            {
                targetButton = await page.QuerySelectorAsync(selector);
                if (targetButton != null) break;
            }

            if (targetButton != null)
            {
                _logger.LogInformation("🎯 Hedef rezervasyon / sepete ekleme butonu bulundu, tıklanıyor...");
                await targetButton.ClickAsync();
                await Task.Delay(2000, cancellationToken);

                var finalUrl = page.Url;
                var expiresAt = DateTime.UtcNow.AddMinutes(10);

                _logger.LogInformation("✅ VIP Bilet başarıyla sepete eklendi! Sepet URL: {FinalUrl}", finalUrl);
                return new ReservationResult(true, finalUrl, expiresAt);
            }

            if (_allowMockFallback)
            {
                _logger.LogInformation("ℹ️ Doğrudan DOM butonu bulunamadı, AllowMockFallback açık olduğu için simülatöre devrediliyor.");
                return await _fallbackEngine.AttemptReserveAsync(hunt, cancellationToken);
            }

            _logger.LogWarning("⚠️ [PlaywrightTicketReservationEngine] Sepete ekleme butonu bulunamadı: {Url}", hunt.RouteOrEventId);
            return new ReservationResult(false, null, null, "Sepete ekleme butonu sayfada bulunamadı.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Playwright üzerinden sepete ekleme sırasında istisna oluştu: {Url}", hunt.RouteOrEventId);
            if (_allowMockFallback)
            {
                return await _fallbackEngine.AttemptReserveAsync(hunt, cancellationToken);
            }

            return new ReservationResult(false, null, null, ex.Message);
        }
    }

    private async Task TrySelectPreferredCategoryAsync(IPage page, string category)
    {
        try
        {
            var escaped = category.Replace("'", "\\'");
            var categorySelectors = new[]
            {
                $"button:has-text('{escaped}')",
                $"a:has-text('{escaped}')",
                $"[role='button']:has-text('{escaped}')",
                $"label:has-text('{escaped}')"
            };

            foreach (var selector in categorySelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    await element.ClickAsync();
                    await Task.Delay(500);
                    _logger.LogInformation("🎫 Tercih edilen kategori seçildi: {Category}", category);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Kategori seçimi uygulanamadı ({Category}), genel akışla devam ediliyor.", category);
        }
    }
}
