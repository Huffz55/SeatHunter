namespace SeatHunter.Application.Interfaces;

public enum CaptchaType
{
    CloudflareTurnstile = 0,
    ReCaptchaV2 = 1,
    ReCaptchaV3 = 2,
    HCaptcha = 3
}

public record CaptchaSolveRequest(
    CaptchaType Type,
    string PageUrl,
    string SiteKey,
    string? Action = null
);

public record CaptchaSolveResult(
    bool IsSuccess,
    string? SolutionToken = null,
    string? ErrorMessage = null,
    double DurationMs = 0
);

public interface ICaptchaSolver
{
    Task<CaptchaSolveResult> SolveAsync(CaptchaSolveRequest request, CancellationToken cancellationToken = default);
    bool IsConfigured { get; }
}
