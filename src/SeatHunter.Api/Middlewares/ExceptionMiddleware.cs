using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace SeatHunter.Api.Middlewares;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sistemde beklenmeyen bir hata oluştu.");
            await HandleExceptionAsync(httpContext, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        if (exception is ArgumentException argEx)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            var badRequestResponse = new
            {
                StatusCode = context.Response.StatusCode,
                Message = argEx.Message,
                Detailed = _environment.IsDevelopment() ? argEx.ToString() : null
            };
            return context.Response.WriteAsync(JsonSerializer.Serialize(badRequestResponse));
        }

        if (exception is Domain.Exceptions.PlanQuotaExceededException quotaEx)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            var quotaResponse = new
            {
                StatusCode = context.Response.StatusCode,
                ErrorCode = "PLAN_QUOTA_EXCEEDED",
                Tier = quotaEx.Tier,
                MaxAllowed = quotaEx.MaxAllowed,
                Message = quotaEx.Message
            };
            return context.Response.WriteAsync(JsonSerializer.Serialize(quotaResponse));
        }

        if (exception is Domain.Exceptions.FeatureGateException featureEx)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            var featureResponse = new
            {
                StatusCode = context.Response.StatusCode,
                ErrorCode = "FEATURE_LOCKED",
                FeatureName = featureEx.FeatureName,
                RequiredTier = featureEx.RequiredTier,
                Message = featureEx.Message
            };
            return context.Response.WriteAsync(JsonSerializer.Serialize(featureResponse));
        }

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            StatusCode = context.Response.StatusCode,
            Message = "Sunucu tarafında beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyiniz.",
            Detailed = _environment.IsDevelopment() ? exception.ToString() : null
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
