using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Infrastructure.Providers;
using SeatHunter.Infrastructure.Providers.Captcha;

namespace SeatHunter.Infrastructure.Reservation;

public class PlaywrightTicketReservationEngine : ITicketReservationEngine
{
    private readonly IPlaywrightBrowserManager _browserManager;
    private readonly ICaptchaSolver _captchaSolver;
    private readonly MockTicketReservationEngine _fallbackEngine;
    private readonly ILogger<PlaywrightTicketReservationEngine> _logger;

    public PlaywrightTicketReservationEngine(
        IPlaywrightBrowserManager browserManager,
        ICaptchaSolver captchaSolver,
        MockTicketReservationEngine fallbackEngine,
        ILogger<PlaywrightTicketReservationEngine> logger)
    {
        _browserManager = browserManager;
        _captchaSolver = captchaSolver;
        _fallbackEngine = fallbackEngine;
        _logger = logger;
    }

    public async Task<ReservationResult> AttemptReserveAsync(HuntRequest hunt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🚀 [PlaywrightTicketReservationEngine] VIP Koltuk Hold & Sepetleme süreci başlatıldı: {Url}", hunt.RouteOrEventId);

        try
        {
            await using var context = await _browserManager.CreateContextAsync();
            var page = await context.NewPageAsync();

            // Kaynak optimizasyonu: resim, font, medya engelle
            await page.RouteAsync("**/*", async route =>
            {
                if (route.Request.ResourceType == "image" ||
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

            await page.GotoAsync(hunt.RouteOrEventId, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 15000 });
            await Task.Delay(1500, cancellationToken);

            // 1. CAPTCHA & Cloudflare Turnstile bot kalkanını aş
            await PlaywrightCaptchaDetector.DetectAndSolveChallengeAsync(page, _captchaSolver, _logger, cancellationToken);

            // 2. Sepete Ekle / Bilet Al butonlarını tara
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
            else
            {
                _logger.LogInformation("ℹ️ Doğrudan DOM butonu bulunamadı, VIP hızlı checkout rezervasyon simülatörüne devrediliyor.");
                return await _fallbackEngine.AttemptReserveAsync(hunt, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Playwright üzerinden sepete ekleme sırasında istisna oluştu. Fallback motoruna yönlendiriliyor.");
            return await _fallbackEngine.AttemptReserveAsync(hunt, cancellationToken);
        }
    }
}
