using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Infrastructure.Messaging;
using SeatHunter.Infrastructure.Persistence;
using SeatHunter.Infrastructure.Persistence.Repositories;
using SeatHunter.Infrastructure.Providers;

namespace SeatHunter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SeatHunterDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IHuntRequestRepository, HuntRequestRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRouteRepository, RouteRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        var userAgents = new[] 
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36", // Windows Chrome
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15", // Mac Safari
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1", // iPhone Safari
            "Mozilla/5.0 (Linux; Android 13; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.6099.193 Mobile Safari/537.36", // Android Chrome
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0" // Windows Firefox
        };

        services.AddHttpClient<BiletixScraperProvider>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgents[Random.Shared.Next(userAgents.Length)]);
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
        })
        .AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
        .AddPolicyHandler((sp, request) => HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(5),
                onBreak: (delegateResult, breakDelay) =>
                {
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BiletixScraperProvider>>();
                    var telegram = sp.GetRequiredService<ITelegramService>();
                    logger.LogWarning("⚠️ ACİL: Hedef sağlayıcı yanıt vermiyor! İstekler 5 dakikalığına durduruldu (Circuit Breaker Açık).");
                    _ = telegram.SendMessageAsync("⚠️ ACİL: Hedef sağlayıcı yanıt vermiyor! İstekler 5 dakikalığına durduruldu (Circuit Breaker Açık).");
                },
                onReset: () =>
                {
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BiletixScraperProvider>>();
                    var telegram = sp.GetRequiredService<ITelegramService>();
                    logger.LogInformation("✅ Sistem normale döndü, taramalar yeniden başlıyor.");
                    _ = telegram.SendMessageAsync("✅ Sistem normale döndü, taramalar yeniden başlıyor.");
                }
            ));
            
        services.AddHttpClient<ObiletScraperProvider>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgents[Random.Shared.Next(userAgents.Length)]);
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
        })
        .AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
        .AddPolicyHandler((sp, request) => HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(5),
                onBreak: (delegateResult, breakDelay) =>
                {
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ObiletScraperProvider>>();
                    var telegram = sp.GetRequiredService<ITelegramService>();
                    logger.LogWarning("⚠️ ACİL: Hedef sağlayıcı yanıt vermiyor! İstekler 5 dakikalığına durduruldu (Circuit Breaker Açık).");
                    _ = telegram.SendMessageAsync("⚠️ ACİL: Hedef sağlayıcı yanıt vermiyor! İstekler 5 dakikalığına durduruldu (Circuit Breaker Açık).");
                },
                onReset: () =>
                {
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ObiletScraperProvider>>();
                    var telegram = sp.GetRequiredService<ITelegramService>();
                    logger.LogInformation("✅ Sistem normale döndü, taramalar yeniden başlıyor.");
                    _ = telegram.SendMessageAsync("✅ Sistem normale döndü, taramalar yeniden başlıyor.");
                }
            ));
            
        // RabbitMQ Publisher bağımlılığının IoC'ye eklenmesi
        
        services.AddHttpClient<UniversalProvider>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgents[Random.Shared.Next(userAgents.Length)]);
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
        });

        services.AddScoped<ITicketProviderFactory, TicketProviderFactory>();

        services.AddScoped<IMessagePublisher, RabbitMqPublisher>();

        services.AddStackExchangeRedisCache(options => 
        {
            // Local Docker üzerindeki Redis'e bağlanıyoruz
            options.Configuration = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        });
        services.AddScoped<ICacheService, SeatHunter.Infrastructure.Caching.RedisCacheService>();
        services.AddScoped<IEmailService, SeatHunter.Infrastructure.Services.RealEmailService>();
        services.AddScoped<ITelegramService, SeatHunter.Infrastructure.Services.RealTelegramService>();
        services.AddScoped<IAuthService, SeatHunter.Infrastructure.Services.AuthService>();

        return services;
    }
}
