using SeatHunter.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace SeatHunter.Infrastructure.Providers;

public class TicketProviderFactory : ITicketProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public TicketProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ITicketProvider GetProvider(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) 
            return _serviceProvider.GetRequiredService<UniversalProvider>();

        var lowerUrl = url.ToLowerInvariant();
        if (lowerUrl.Contains("biletix.com"))
        {
            return _serviceProvider.GetRequiredService<BiletixScraperProvider>();
        }
        else if (lowerUrl.Contains("obilet.com"))
        {
            return _serviceProvider.GetRequiredService<ObiletScraperProvider>();
        }

        return _serviceProvider.GetRequiredService<UniversalProvider>();
    }
}
