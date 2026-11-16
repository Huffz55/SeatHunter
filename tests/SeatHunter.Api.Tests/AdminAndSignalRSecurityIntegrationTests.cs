using System.Net;
using FluentAssertions;
using SeatHunter.Domain.Entities;

namespace SeatHunter.Api.Tests;

public class AdminAndSignalRSecurityIntegrationTests : IClassFixture<SeatHunterApiFactory>
{
    private readonly SeatHunterApiFactory _factory;

    public AdminAndSignalRSecurityIntegrationTests(SeatHunterApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminEndpoints_ShouldReturn403ForStandardUser_And200ForAdmin()
    {
        var (userClient, _) = await _factory.RegisterAndCreateClientAsync(
            $"std-{Guid.NewGuid():N}@example.com",
            role: "User");

        var userOverview = await userClient.GetAsync("/api/admin/dashboard/overview");
        var userList = await userClient.GetAsync("/api/admin/users");
        userOverview.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        userList.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (adminClient, _) = await _factory.RegisterAndCreateClientAsync(
            $"adm-{Guid.NewGuid():N}@example.com",
            role: "Admin");

        var adminOverview = await adminClient.GetAsync("/api/admin/dashboard/overview");
        var adminList = await adminClient.GetAsync("/api/admin/users");
        adminOverview.StatusCode.Should().Be(HttpStatusCode.OK);
        adminList.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/notificationHub/negotiate?negotiateVersion=1")]
    [InlineData("/adminHub/negotiate?negotiateVersion=1")]
    public async Task SignalRHubs_ShouldRejectUnauthenticatedRequests(string hubNegotiatePath)
    {
        var anonClient = _factory.CreateClient();
        var res = await anonClient.PostAsync(hubNegotiatePath, content: null);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
