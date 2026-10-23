using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using SeatHunter.Application.Interfaces;
using SeatHunter.Infrastructure.HealthChecks;
using Xunit;

namespace SeatHunter.Infrastructure.Tests;

public class ProxyPoolHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnHealthy_WhenNoProxiesConfigured()
    {
        // Arrange
        var mockProxyService = new Mock<IProxyRotationService>();
        mockProxyService.Setup(p => p.TotalProxies).Returns(0);
        mockProxyService.Setup(p => p.CoolingDownProxiesCount).Returns(0);

        var healthCheck = new ProxyPoolHealthCheck(mockProxyService.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("Doğrudan bağlantı modu aktif");
        result.Data["TotalProxies"].Should().Be(0);
        result.Data["ActiveProxies"].Should().Be(0);
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldReturnHealthy_WhenAllProxiesActive()
    {
        // Arrange
        var mockProxyService = new Mock<IProxyRotationService>();
        mockProxyService.Setup(p => p.TotalProxies).Returns(5);
        mockProxyService.Setup(p => p.CoolingDownProxiesCount).Returns(0);

        var healthCheck = new ProxyPoolHealthCheck(mockProxyService.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("5/5");
        result.Data["ActiveProxies"].Should().Be(5);
        result.Data["CoolingDownProxies"].Should().Be(0);
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldReturnHealthy_WhenSomeProxiesInCooldown()
    {
        // Arrange
        var mockProxyService = new Mock<IProxyRotationService>();
        mockProxyService.Setup(p => p.TotalProxies).Returns(5);
        mockProxyService.Setup(p => p.CoolingDownProxiesCount).Returns(2);

        var healthCheck = new ProxyPoolHealthCheck(mockProxyService.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("3/5 çalışıyor");
        result.Data["ActiveProxies"].Should().Be(3);
        result.Data["CoolingDownProxies"].Should().Be(2);
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldReturnDegraded_WhenAllProxiesInCooldown()
    {
        // Arrange
        var mockProxyService = new Mock<IProxyRotationService>();
        mockProxyService.Setup(p => p.TotalProxies).Returns(3);
        mockProxyService.Setup(p => p.CoolingDownProxiesCount).Returns(3);

        var healthCheck = new ProxyPoolHealthCheck(mockProxyService.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("soğuma modunda");
        result.Data["ActiveProxies"].Should().Be(0);
        result.Data["CoolingDownProxies"].Should().Be(3);
    }
}
