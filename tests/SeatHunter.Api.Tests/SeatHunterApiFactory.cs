using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using SeatHunter.Application.DTOs;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Enums;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Infrastructure.Messaging;
using SeatHunter.Infrastructure.Persistence;
using StackExchange.Redis;

namespace SeatHunter.Api.Tests;

/// <summary>
/// Isolated in-memory SQLite WebApplicationFactory for end-to-end SeatHunter API integration tests.
/// Replaces external Postgres, Redis, RabbitMQ, and notification transport dependencies.
/// </summary>
public class SeatHunterApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestWebhookSecret = "IntegrationTestBillingWebhookSecret32B!";
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public Mock<IMessagePublisher> MessagePublisherMock { get; } = new();
    public Mock<IDistributedHuntScheduler> HuntSchedulerMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", "IntegrationTestSuperSecretJwtKey32Bytes!");
        builder.UseSetting("Billing:WebhookSecret", TestWebhookSecret);
        builder.UseSetting("ConnectionStrings:DefaultConnection", "DataSource=:memory:");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SeatHunterDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SeatHunterDbContext>>();
            services.RemoveAll<SeatHunterDbContext>();

            services.AddDbContext<SeatHunterDbContext>(options => options.UseSqlite(_connection));

            ReplaceWithInstance<IMessagePublisher>(services, MessagePublisherMock.Object);
            ReplaceWithInstance<IDistributedHuntScheduler>(services, HuntSchedulerMock.Object);
            ReplaceWithInstance(services, Mock.Of<IRabbitMqConnectionManager>());
            ReplaceWithInstance(services, Mock.Of<ICacheService>());
            ReplaceWithInstance(services, Mock.Of<IConnectionMultiplexer>());
            ReplaceWithInstance(services, Mock.Of<IEmailService>());
            ReplaceWithInstance(services, Mock.Of<ITelegramService>());
            ReplaceWithInstance(services, Mock.Of<IDiscordService>());

            var dlqMock = new Mock<IDeadLetterReplayService>();
            dlqMock.Setup(r => r.GetStatusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeadLetterQueueStatus("seathunter_dlq", 0, 0, DateTime.UtcNow));
            ReplaceWithInstance(services, dlqMock.Object);
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeatHunterDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task<(HttpClient Client, AuthResultDto Auth)> RegisterAndCreateClientAsync(
        string email,
        string password = "Password123!",
        string firstName = "Test",
        string lastName = "Hunter",
        UserTier tier = UserTier.Free,
        string role = "User")
    {
        var client = CreateClient();
        var regResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            firstName,
            lastName
        });
        regResponse.EnsureSuccessStatusCode();

        if (tier != UserTier.Free || role != "User")
        {
            await UpdateUserInDatabaseAsync(email, user =>
            {
                user.Tier = tier;
                user.Role = role;
            });
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();

        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResultDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth);
    }

    public async Task UpdateUserInDatabaseAsync(string email, Action<User> update)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeatHunterDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        update(user);
        await db.SaveChangesAsync();
    }

    public async Task SeedPriceSnapshotAsync(string routeOrEventId, decimal price, DateTime? recordedAt = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeatHunterDbContext>();
        db.PriceSnapshots.Add(new PriceSnapshot
        {
            RouteOrEventId = routeOrEventId,
            ProviderName = "Universal",
            EventName = "Test Etkinliği",
            Price = price,
            Currency = "TRY",
            RecordedAt = recordedAt ?? DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static void ReplaceWithInstance<TService>(IServiceCollection services, TService instance)
        where TService : class
    {
        services.RemoveAll<TService>();
        services.AddSingleton(instance);
    }
}
