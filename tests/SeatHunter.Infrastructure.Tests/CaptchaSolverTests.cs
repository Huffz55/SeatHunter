using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using SeatHunter.Application.Interfaces;
using SeatHunter.Infrastructure.Providers.Captcha;
using Xunit;

namespace SeatHunter.Infrastructure.Tests;

public class CaptchaSolverTests
{
    private readonly Mock<ILogger<MockCaptchaSolver>> _mockLogger = new();
    private readonly Mock<ILogger<CapSolverService>> _mockCapSolverLogger = new();

    [Fact]
    public async Task MockCaptchaSolver_ShouldReturnValidToken()
    {
        // Arrange
        var solver = new MockCaptchaSolver(_mockLogger.Object);
        var request = new CaptchaSolveRequest(CaptchaType.CloudflareTurnstile, "https://biletix.com", "0x4AAAAAA...");

        // Act
        var result = await solver.SolveAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.SolutionToken);
        Assert.StartsWith("mock_solved_token_", result.SolutionToken);
        Assert.True(solver.IsConfigured);
    }

    [Fact]
    public async Task CapSolverService_WhenNoApiKey_ShouldFallbackToMockSolver()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Captcha:ApiKey", null}
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var mockSolver = new MockCaptchaSolver(_mockLogger.Object);
        var httpClient = new HttpClient();
        var capSolver = new CapSolverService(httpClient, configuration, mockSolver, _mockCapSolverLogger.Object);

        var request = new CaptchaSolveRequest(CaptchaType.ReCaptchaV2, "https://example.com", "6Ld_site_key");

        // Act
        Assert.False(capSolver.IsConfigured);
        var result = await capSolver.SolveAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.SolutionToken);
        Assert.StartsWith("mock_solved_token_", result.SolutionToken);
    }

    [Fact]
    public async Task CapSolverService_WhenApiKeyConfigured_AndApiReturnsReady_ShouldReturnToken()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Captcha:ApiKey", "CA-TEST-API-KEY-123"}
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        // 1. createTask call
        var createTaskResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                errorId = 0,
                taskId = "task_98765"
            }))
        };

        // 2. getTaskResult call
        var getTaskResultResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                errorId = 0,
                status = "ready",
                solution = new
                {
                    token = "cf_turnstile_real_token_xyz"
                }
            }))
        };

        handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(createTaskResponse)
            .ReturnsAsync(getTaskResultResponse);

        var httpClient = new HttpClient(handlerMock.Object);
        var mockSolver = new MockCaptchaSolver(_mockLogger.Object);
        var capSolver = new CapSolverService(httpClient, configuration, mockSolver, _mockCapSolverLogger.Object);

        var request = new CaptchaSolveRequest(CaptchaType.CloudflareTurnstile, "https://ticketmaster.com", "0x4AAAAAA-KEY");

        // Act
        Assert.True(capSolver.IsConfigured);
        var result = await capSolver.SolveAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("cf_turnstile_real_token_xyz", result.SolutionToken);
    }

    [Fact]
    public async Task CapSolverService_WhenApiReturnsError_ShouldReturnFailure()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Captcha:ApiKey", "CA-TEST-API-KEY-123"}
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        var errorResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                errorId = 1,
                errorDescription = "ERROR_ZERO_BALANCE"
            }))
        };

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(errorResponse);

        var httpClient = new HttpClient(handlerMock.Object);
        var mockSolver = new MockCaptchaSolver(_mockLogger.Object);
        var capSolver = new CapSolverService(httpClient, configuration, mockSolver, _mockCapSolverLogger.Object);

        var request = new CaptchaSolveRequest(CaptchaType.ReCaptchaV2, "https://ticketmaster.com", "sitekey-123");

        // Act
        var result = await capSolver.SolveAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.SolutionToken);
        Assert.Equal("ERROR_ZERO_BALANCE", result.ErrorMessage);
    }
}
