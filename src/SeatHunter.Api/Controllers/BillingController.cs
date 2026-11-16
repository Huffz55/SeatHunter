using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Constants;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Api.Controllers;

[ApiController]
[Route("api/billing")]
[EnableRateLimiting(AppConstants.RateLimiting.FixedPolicy)]
public class BillingController : ControllerBase
{
    private static readonly JsonSerializerOptions WebhookJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IBillingService _billingService;
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _environment;

    public BillingController(
        IBillingService billingService,
        IAuthService authService,
        IWebHostEnvironment environment)
    {
        _billingService = billingService;
        _authService = authService;
        _environment = environment;
    }

    [AllowAnonymous]
    [HttpGet("plans")]
    public IActionResult GetPlans()
    {
        var plans = _billingService.GetPlansCatalog().Select(p => new
        {
            Tier = p.Tier.ToString(),
            p.Name,
            p.MonthlyPriceTry,
            p.YearlyPriceTry,
            p.Currency,
            p.MaxActiveHunts,
            p.MinScanIntervalMinutes,
            p.AllowAdvancedCriteria,
            p.AllowAutoReservation
        });

        return Ok(plans);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyBillingStatus(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var status = await _billingService.GetUserBillingStatusAsync(userId, cancellationToken);
            return Ok(status);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("checkout")]
    public async Task<IActionResult> StartCheckout(
        [FromBody] StartCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (!Enum.TryParse<UserTier>(request.TargetTier, true, out var targetTier))
            return BadRequest(new { message = "Geçersiz hedef plan seçimi." });

        if (!Enum.TryParse<BillingCycle>(request.BillingCycle, true, out var cycle))
            return BadRequest(new { message = "Geçersiz faturalama dönemi (Monthly veya Yearly olmalı)." });

        try
        {
            var session = await _billingService.StartCheckoutAsync(
                userId,
                new StartCheckoutCommand(
                    TargetTier: targetTier,
                    Cycle: cycle,
                    IdempotencyKey: request.IdempotencyKey,
                    SuccessRedirectUrl: request.SuccessRedirectUrl,
                    CancelRedirectUrl: request.CancelRedirectUrl),
                cancellationToken);

            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("sandbox/confirm/{externalSessionId}")]
    public async Task<IActionResult> ConfirmSandboxCheckout(
        string externalSessionId,
        CancellationToken cancellationToken)
    {
        if (_environment.IsProduction())
        {
            return NotFound(new { message = "Sandbox ödeme onayı production ortamında devre dışıdır." });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var result = await _billingService.ConfirmSandboxCheckoutAsync(userId, externalSessionId, cancellationToken);
            var refreshedAuth = await _authService.RefreshSessionAsync(userId, cancellationToken);

            return Ok(new
            {
                result.Succeeded,
                result.EffectiveTier,
                result.SubscriptionExpiresAtUtc,
                result.Message,
                Token = refreshedAuth?.Token,
                Auth = refreshedAuth
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return ForbidOrUnauthorized(ex.Message);
        }
    }

    [Authorize]
    [HttpPost("cancel")]
    public async Task<IActionResult> CancelAutoRenew(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var result = await _billingService.CancelAutoRenewAsync(userId, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawPayload = await reader.ReadToEndAsync(cancellationToken);

        BillingWebhookEvent? webhookEvent;
        try
        {
            webhookEvent = JsonSerializer.Deserialize<BillingWebhookEvent>(rawPayload, WebhookJsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Geçersiz webhook JSON gövdesi." });
        }

        if (webhookEvent == null || string.IsNullOrWhiteSpace(webhookEvent.ExternalSessionId))
        {
            return BadRequest(new { message = "ExternalSessionId alanı zorunludur." });
        }

        var signature = Request.Headers["X-Billing-Signature"].FirstOrDefault();

        try
        {
            var result = await _billingService.ProcessWebhookAsync(webhookEvent, rawPayload, signature, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    private IActionResult ForbidOrUnauthorized(string message) =>
        StatusCode(StatusCodes.Status403Forbidden, new { message });
}

public class StartCheckoutRequest
{
    public string TargetTier { get; set; } = "Pro";
    public string BillingCycle { get; set; } = "Monthly";
    public string? IdempotencyKey { get; set; }
    public string? SuccessRedirectUrl { get; set; }
    public string? CancelRedirectUrl { get; set; }
}
