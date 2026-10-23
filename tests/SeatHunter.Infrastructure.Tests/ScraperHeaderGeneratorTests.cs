using FluentAssertions;
using SeatHunter.Infrastructure.Providers.Headers;
using Xunit;

namespace SeatHunter.Infrastructure.Tests;

public class ScraperHeaderGeneratorTests
{
    [Fact]
    public void ApplyBrowserHeaders_ShouldPopulateRequiredEvasionHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://biletix.com");

        // Act
        ScraperHeaderGenerator.ApplyBrowserHeaders(request);

        // Assert
        request.Headers.UserAgent.Should().NotBeNull();
        request.Headers.Contains("Accept").Should().BeTrue();
        request.Headers.Contains("Accept-Language").Should().BeTrue();
        request.Headers.Contains("Sec-Ch-Ua").Should().BeTrue();
        request.Headers.Contains("Sec-Ch-Ua-Mobile").Should().BeTrue();
        request.Headers.Contains("Sec-Ch-Ua-Platform").Should().BeTrue();
        request.Headers.Contains("Sec-Fetch-Dest").Should().BeTrue();
        request.Headers.Contains("Sec-Fetch-Mode").Should().BeTrue();
        request.Headers.Contains("Sec-Fetch-Site").Should().BeTrue();
        request.Headers.Contains("Sec-Fetch-User").Should().BeTrue();
    }
}
