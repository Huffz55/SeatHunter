using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Infrastructure.Providers.Captcha;

public static class PlaywrightCaptchaDetector
{
    private static readonly Regex CloudflareSiteKeyRegex = new(@"sitekey=([a-zA-Z0-9_-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ReCaptchaSiteKeyRegex = new(@"k=([a-zA-Z0-9_-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static async Task<bool> DetectAndSolveChallengeAsync(
        IPage page, 
        ICaptchaSolver solver, 
        ILogger? logger, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await page.ContentAsync();

            // 1. Cloudflare Turnstile Challenge Tespiti
            bool isTurnstile = content.Contains("challenges.cloudflare.com", StringComparison.OrdinalIgnoreCase) ||
                               content.Contains("cf-turnstile", StringComparison.OrdinalIgnoreCase) ||
                               content.Contains("Checking if the site connection is secure", StringComparison.OrdinalIgnoreCase) ||
                               content.Contains("Just a moment...", StringComparison.OrdinalIgnoreCase);

            if (isTurnstile)
            {
                logger?.LogWarning("🛡️ Cloudflare Turnstile bot koruma kalkanı tespit edildi: {Url}", page.Url);

                string? siteKey = null;
                var turnstileElem = await page.QuerySelectorAsync("div.cf-turnstile, [data-sitekey]");
                if (turnstileElem != null)
                {
                    siteKey = await turnstileElem.GetAttributeAsync("data-sitekey");
                }

                if (string.IsNullOrEmpty(siteKey))
                {
                    var iframeElem = await page.QuerySelectorAsync("iframe[src*='challenges.cloudflare.com']");
                    if (iframeElem != null)
                    {
                        var src = await iframeElem.GetAttributeAsync("src");
                        if (!string.IsNullOrEmpty(src))
                        {
                            var match = CloudflareSiteKeyRegex.Match(src);
                            if (match.Success) siteKey = match.Groups[1].Value;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(siteKey))
                {
                    logger?.LogInformation("🧩 Turnstile SiteKey yakalandı: {SiteKey}. Çözücüye devrediliyor...", siteKey);
                    var solveResult = await solver.SolveAsync(
                        new CaptchaSolveRequest(CaptchaType.CloudflareTurnstile, page.Url, siteKey), 
                        cancellationToken);

                    if (solveResult.IsSuccess && !string.IsNullOrEmpty(solveResult.SolutionToken))
                    {
                        logger?.LogInformation("🔓 Turnstile token başarıyla alındı, sayfaya enjekte ediliyor...");
                        
                        await page.EvaluateAsync(@"(token) => {
                            let input = document.querySelector('input[name=""cf-turnstile-response""]');
                            if (!input) {
                                input = document.createElement('input');
                                input.type = 'hidden';
                                input.name = 'cf-turnstile-response';
                                document.body.appendChild(input);
                            }
                            input.value = token;

                            if (window.turnstile && typeof window.turnstile.reset === 'function') {
                                try { window.turnstile.reset(); } catch(e){}
                            }
                        }", solveResult.SolutionToken);

                        await Task.Delay(1000, cancellationToken);
                        return true;
                    }
                }
            }

            // 2. Google ReCaptcha V2 / V3 Tespiti
            bool isRecaptcha = content.Contains("google.com/recaptcha", StringComparison.OrdinalIgnoreCase) ||
                               content.Contains("g-recaptcha", StringComparison.OrdinalIgnoreCase);

            if (isRecaptcha)
            {
                logger?.LogWarning("🛡️ Google reCAPTCHA meydan okuması tespit edildi: {Url}", page.Url);

                string? siteKey = null;
                var recaptchaElem = await page.QuerySelectorAsync(".g-recaptcha, [data-sitekey]");
                if (recaptchaElem != null)
                {
                    siteKey = await recaptchaElem.GetAttributeAsync("data-sitekey");
                }

                if (!string.IsNullOrEmpty(siteKey))
                {
                    var solveResult = await solver.SolveAsync(
                        new CaptchaSolveRequest(CaptchaType.ReCaptchaV2, page.Url, siteKey), 
                        cancellationToken);

                    if (solveResult.IsSuccess && !string.IsNullOrEmpty(solveResult.SolutionToken))
                    {
                        await page.EvaluateAsync(@"(token) => {
                            let textarea = document.querySelector('textarea[name=""g-recaptcha-response""]');
                            if (!textarea) {
                                textarea = document.createElement('textarea');
                                textarea.name = 'g-recaptcha-response';
                                textarea.style.display = 'none';
                                document.body.appendChild(textarea);
                            }
                            textarea.value = token;
                        }", solveResult.SolutionToken);

                        await Task.Delay(1000, cancellationToken);
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "CAPTCHA tespit ve atlatma sürecinde hata meydana geldi.");
        }

        return false;
    }
}
