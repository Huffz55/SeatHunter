using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SeatHunter.Infrastructure.Providers.Proxy;
using Xunit;

namespace SeatHunter.Infrastructure.Tests;

public class ProxyRotationServiceTests
{
    [Fact]
    public void GetNextProxy_ShouldReturnNull_WhenNoProxiesConfigured()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "SCRAPER_PROXIES", "" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var mockLogger = new Mock<ILogger<ProxyRotationService>>();
        var service = new ProxyRotationService(config, mockLogger.Object);

        // Act
        var result = service.GetNextProxy();

        // Assert
        result.Should().BeNull();
        service.HasProxies.Should().BeFalse();
    }

    [Fact]
    public void GetNextProxy_ShouldRotateInRoundRobin_WhenMultipleProxiesConfigured()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "SCRAPER_PROXIES", "http://proxy1.com:8080, http://user:pass@proxy2.com:9090" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var mockLogger = new Mock<ILogger<ProxyRotationService>>();
        var service = new ProxyRotationService(config, mockLogger.Object);

        // Act
        var p1 = service.GetNextProxy();
        var p2 = service.GetNextProxy();
        var p3 = service.GetNextProxy();

        // Assert
        service.HasProxies.Should().BeTrue();
        p1.Should().NotBeNull();
        p2.Should().NotBeNull();
        p3.Should().NotBeNull();

        p1!.Server.Should().Be("http://proxy1.com:8080");
        p2!.Server.Should().Be("http://proxy2.com:9090");
        p2.Username.Should().Be("user");
        p2.Password.Should().Be("pass");
        p3!.Server.Should().Be("http://proxy1.com:8080"); // Round-robin loops back
    }

    [Fact]
    public void ReportFailure_ShouldTriggerCooldown_AfterThreeFailures()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "SCRAPER_PROXIES", "http://failing.com:8080, http://working.com:8080" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var mockLogger = new Mock<ILogger<ProxyRotationService>>();
        var service = new ProxyRotationService(config, mockLogger.Object);

        // Act: 3 kez hata bildiriyoruz
        service.ReportFailure("http://failing.com:8080");
        service.ReportFailure("http://failing.com:8080");
        service.ReportFailure("http://failing.com:8080");

        // Assert: Artık çalışan proxy öncelikli seçilmeli
        var selected = service.GetNextProxy();
        selected.Should().NotBeNull();
        selected!.Server.Should().Be("http://working.com:8080");
        service.TotalProxies.Should().Be(2);
        service.CoolingDownProxiesCount.Should().Be(1);
    }
}
