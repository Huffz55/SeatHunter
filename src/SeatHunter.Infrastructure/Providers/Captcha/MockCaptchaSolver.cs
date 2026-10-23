using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Infrastructure.Providers.Captcha;

public class MockCaptchaSolver : ICaptchaSolver
{
    private readonly ILogger<MockCaptchaSolver> _logger;

    public MockCaptchaSolver(ILogger<MockCaptchaSolver> logger)
    {
        _logger = logger;
    }

    public bool IsConfigured => true;

    public Task<CaptchaSolveResult> SolveAsync(CaptchaSolveRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🤖 [MockCaptchaSolver] {Type} meydan okuması simüle edilerek çözülüyor: {Url} (SiteKey: {Key})",
            request.Type, request.PageUrl, request.SiteKey);

        var token = $"mock_solved_token_{Guid.NewGuid():N}";
        return Task.FromResult(new CaptchaSolveResult(
            IsSuccess: true,
            SolutionToken: token,
            DurationMs: 150
        ));
    }
}
