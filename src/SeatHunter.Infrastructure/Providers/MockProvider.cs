using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Interfaces;

namespace SeatHunter.Infrastructure.Providers;

public class MockProvider : ITicketProvider
{
    private readonly ICacheService _cacheService;

    public MockProvider(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public string ProviderName => "MockProvider";

    public async Task<bool> CheckAvailabilityAsync(string url, CancellationToken cancellationToken)
    {
        string cacheKey = $"availability_{ProviderName}_{url}";

        // Önce Redis Cache'e bakıyoruz (Rate limiting / maliyet optimizasyonu simülasyonu)
        var cachedResult = await _cacheService.GetAsync<bool?>(cacheKey, cancellationToken);
        if (cachedResult.HasValue)
        {
            // Eğer cache'de varsa saniyeler sürecek dış servise gitmek yerine anında dönüyoruz!
            return cachedResult.Value;
        }

        // Gerçek bir dış API / web scraping işlemini simüle etmek için 2 saniye bekletiyoruz
        await Task.Delay(2000, cancellationToken);
        
        // Mülakat senaryoları ve testler için %20 ihtimalle boş koltuk bulundu (true) dönüyoruz
        var random = new Random();
        bool isAvailable = random.Next(1, 100) <= 20; 

        // Bulduğumuz sonucu 1 dakika boyunca Redis'te saklıyoruz ki aynı rotaya peş peşe gelen istekler dış servisi yormasın
        await _cacheService.SetAsync(cacheKey, isAvailable, TimeSpan.FromMinutes(1), cancellationToken);

        return isAvailable;
    }

    public Task<string> ExtractEventNameAsync(string url, CancellationToken cancellationToken)
    {
        return Task.FromResult("Mock Etkinlik");
    }
}
