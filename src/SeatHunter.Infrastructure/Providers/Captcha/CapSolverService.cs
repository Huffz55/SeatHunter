using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Infrastructure.Providers.Captcha;

public class CapSolverService : ICaptchaSolver
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly ICaptchaSolver _fallbackSolver;
    private readonly ILogger<CapSolverService> _logger;

    public CapSolverService(
        HttpClient httpClient,
        IConfiguration configuration,
        MockCaptchaSolver fallbackSolver,
        ILogger<CapSolverService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Captcha:ApiKey"] ?? configuration["CAPSOLVER_API_KEY"];
        _fallbackSolver = fallbackSolver;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<CaptchaSolveResult> SolveAsync(CaptchaSolveRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogDebug("CapSolver API anahtarı tanımlanmamış. Mock çözücüye devrediliyor.");
            return await _fallbackSolver.SolveAsync(request, cancellationToken);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var taskType = request.Type switch
            {
                CaptchaType.CloudflareTurnstile => "AntiTurnstileTaskProxyLess",
                CaptchaType.ReCaptchaV2 => "ReCaptchaV2TaskProxyLess",
                CaptchaType.ReCaptchaV3 => "ReCaptchaV3TaskProxyLess",
                CaptchaType.HCaptcha => "HCaptchaTaskProxyLess",
                _ => "AntiTurnstileTaskProxyLess"
            };

            var payload = new
            {
                clientKey = _apiKey,
                task = new
                {
                    type = taskType,
                    websiteURL = request.PageUrl,
                    websiteKey = request.SiteKey,
                    pageAction = request.Action
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.capsolver.com/createTask", jsonContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                sw.Stop();
                return new CaptchaSolveResult(false, null, $"CapSolver HTTP {(int)response.StatusCode}", sw.Elapsed.TotalMilliseconds);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("errorId", out var errorIdProp) && errorIdProp.GetInt32() != 0)
            {
                var errorDesc = root.TryGetProperty("errorDescription", out var descProp) ? descProp.GetString() : "Unknown error";
                sw.Stop();
                return new CaptchaSolveResult(false, null, errorDesc, sw.Elapsed.TotalMilliseconds);
            }

            if (!root.TryGetProperty("taskId", out var taskIdProp))
            {
                sw.Stop();
                return new CaptchaSolveResult(false, null, "taskId bulunamadı", sw.Elapsed.TotalMilliseconds);
            }

            var taskId = taskIdProp.GetString();

            // Görev sonucunu sorgulama döngüsü (Maksimum 30 saniye)
            var pollPayload = new { clientKey = _apiKey, taskId };
            for (int i = 0; i < 15; i++)
            {
                await Task.Delay(2000, cancellationToken);

                var pollContent = new StringContent(JsonSerializer.Serialize(pollPayload), Encoding.UTF8, "application/json");
                var pollResponse = await _httpClient.PostAsync("https://api.capsolver.com/getTaskResult", pollContent, cancellationToken);
                
                if (!pollResponse.IsSuccessStatusCode) continue;

                var pollBody = await pollResponse.Content.ReadAsStringAsync(cancellationToken);
                using var pollDoc = JsonDocument.Parse(pollBody);
                var pollRoot = pollDoc.RootElement;

                if (pollRoot.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "ready")
                {
                    sw.Stop();
                    string? token = null;

                    if (pollRoot.TryGetProperty("solution", out var solutionProp))
                    {
                        if (solutionProp.TryGetProperty("token", out var tokenProp))
                            token = tokenProp.GetString();
                        else if (solutionProp.TryGetProperty("gRecaptchaResponse", out var gProp))
                            token = gProp.GetString();
                    }

                    if (!string.IsNullOrEmpty(token))
                    {
                        _logger.LogInformation("✅ CAPTCHA {Type} başarıyla çözüldü ({Duration:F1}ms).", request.Type, sw.Elapsed.TotalMilliseconds);
                        return new CaptchaSolveResult(true, token, null, sw.Elapsed.TotalMilliseconds);
                    }
                }
            }

            sw.Stop();
            return new CaptchaSolveResult(false, null, "Çözüm zaman aşımına uğradı", sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "CAPTCHA çözümü sırasında beklenmeyen hata oluştu.");
            return new CaptchaSolveResult(false, null, ex.Message, sw.Elapsed.TotalMilliseconds);
        }
    }
}
