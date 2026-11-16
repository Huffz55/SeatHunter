using System.Net;
using FluentAssertions;

namespace SeatHunter.Api.Tests;

public class ObservabilityIntegrationTests : IClassFixture<SeatHunterApiFactory>
{
    private readonly SeatHunterApiFactory _factory;

    public ObservabilityIntegrationTests(SeatHunterApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LiveHealthEndpoint_ShouldReturnHealthy()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health/live");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Be("Healthy");
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldExposePrometheusMetrics()
    {
        var client = _factory.CreateClient();
        // Bir istek üreterek HTTP metrik serisinin oluşmasını sağla
        await client.GetAsync("/health/live");

        var res = await client.GetAsync("/metrics");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("# TYPE ");
        body.Should().Contain("http_server_");
    }

    [Fact]
    public async Task CorrelationIdMiddleware_ShouldEchoProvidedHeader_OrGenerateNewOne()
    {
        var client = _factory.CreateClient();

        using var customReq = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        customReq.Headers.Add("X-Correlation-ID", "test-corr-123");
        var customRes = await client.SendAsync(customReq);

        customRes.Headers.TryGetValues("X-Correlation-ID", out var echoed).Should().BeTrue();
        echoed!.Should().ContainSingle().Which.Should().Be("test-corr-123");

        var autoRes = await client.GetAsync("/health/live");
        autoRes.Headers.TryGetValues("X-Correlation-ID", out var generated).Should().BeTrue();
        generated!.Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CompiledFrontendBundle_ShouldBeServedFromWwwrootWithoutCdnTranspilers()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/index.html");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await res.Content.ReadAsStringAsync();
        html.Should().Contain("<script type=\"module\"");
        html.Should().NotContain("unpkg.com/@babel/standalone");
        html.Should().NotContain("cdn.tailwindcss.com");
    }
}
