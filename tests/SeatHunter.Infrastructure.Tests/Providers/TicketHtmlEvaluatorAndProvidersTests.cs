using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Infrastructure.Providers;
using Xunit;

namespace SeatHunter.Infrastructure.Tests.Providers;

public class TicketHtmlEvaluatorAndProvidersTests
{
    private sealed class StubMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class StubHttpProvider : HttpScraperProviderBase
    {
        public StubHttpProvider(HttpClient client, ITicketProvider? fallback = null)
            : base(client, fallback)
        {
        }

        public override string ProviderName => "StubProvider";
        protected override IReadOnlyList<string> PositiveKeywords => ["Bilet Al", "Sepete Ekle"];
        protected override IReadOnlyList<string> NegativeKeywords => ["Tükendi"];
    }

    [Fact]
    public void Evaluate_WhenCategoryAvailable_ShouldMatchCategoryAndExtractCategoryPrice()
    {
        var html = @"
            <html><body>
                <div>Tribün 2 - Bilet Al - 1.250 TL</div>
                <div>Saha İçi - Bilet Al - 2.400 TL</div>
            </body></html>";

        var result = TicketHtmlEvaluator.Evaluate(
            html,
            ["Bilet Al"],
            ["Tükendi"],
            preferredCategory: "Saha İçi");

        result.IsAvailable.Should().BeTrue();
        result.CategoryMatched.Should().BeTrue();
        result.Price.Should().Be(2400m);
    }

    [Fact]
    public void Evaluate_WhenPreferredCategorySoldOutWhileAnotherCategoryAvailable_ShouldReturnFalse()
    {
        var html = @"
            <html><body>
                <div>Balkon - Bilet Al - 900 TL</div>
                <div>Saha İçi - Tükendi</div>
            </body></html>";

        var result = TicketHtmlEvaluator.Evaluate(
            html,
            ["Bilet Al"],
            ["Satışa Kapalı"],
            preferredCategory: "Saha İçi");

        result.IsAvailable.Should().BeFalse();
        result.CategoryMatched.Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "Access Denied", true)]
    [InlineData(HttpStatusCode.TooManyRequests, "Rate limited", true)]
    [InlineData(HttpStatusCode.OK, "<html><body>Checking your browser before accessing...</body></html>", true)]
    [InlineData(HttpStatusCode.OK, "<html><body><button>Bilet Al</button></body></html>", false)]
    public void RequiresBrowserRendering_ShouldDetectBotBlocksAndChallenges(
        HttpStatusCode statusCode,
        string html,
        bool expected)
    {
        TicketHtmlEvaluator.RequiresBrowserRendering(statusCode, html).Should().Be(expected);
    }

    [Fact]
    public async Task HttpScraperProviderBase_WhenBlockedWith403_ShouldFallbackToBrowserProviderInSingleCall()
    {
        var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("cf-turnstile", Encoding.UTF8, "text/html")
        });
        var client = new HttpClient(handler);

        var mockFallback = new Mock<ITicketProvider>();
        mockFallback
            .Setup(f => f.InspectAvailabilityAsync("https://biletix.com/event/1", "VIP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketInspectionResult(true, 1850m, true));

        var provider = new StubHttpProvider(client, mockFallback.Object);

        var result = await provider.InspectAvailabilityAsync("https://biletix.com/event/1", "VIP", CancellationToken.None);

        result.IsAvailable.Should().BeTrue();
        result.Price.Should().Be(1850m);
        result.CategoryMatched.Should().BeTrue();
        mockFallback.Verify(f => f.InspectAvailabilityAsync("https://biletix.com/event/1", "VIP", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HttpScraperProviderBase_WhenHttp200_ShouldPerformSingleHttpRequestForAvailabilityAndPrice()
    {
        int callCount = 0;
        var handler = new StubMessageHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body><h1>Konser</h1><span>Bilet Al 1.150 TL</span></body></html>", Encoding.UTF8, "text/html")
            };
        });
        var provider = new BiletixScraperProvider(new HttpClient(handler));

        var result = await provider.InspectAvailabilityAsync("https://biletix.com/event/2", null, CancellationToken.None);

        callCount.Should().Be(1);
        result.IsAvailable.Should().BeTrue();
        result.Price.Should().Be(1150m);
    }
}
