using FluentAssertions;
using Moq;
using SeatHunter.Application.Features.HuntRequests.Commands.CreateHuntRequest;
using SeatHunter.Application.Interfaces;
using SeatHunter.Application.Services;
using SeatHunter.Domain.Constants;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Enums;
using SeatHunter.Domain.Exceptions;
using Xunit;

namespace SeatHunter.Application.Tests.Services;

public class UserPlanServiceTests
{
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IHuntRequestRepository> _mockHuntRepo;
    private readonly UserPlanService _service;

    public UserPlanServiceTests()
    {
        _mockUserRepo = new Mock<IUserRepository>();
        _mockHuntRepo = new Mock<IHuntRequestRepository>();
        _service = new UserPlanService(_mockUserRepo.Object, _mockHuntRepo.Object);
    }

    [Fact]
    public void GetLimitsForTier_ShouldReturnCorrectLimits_ForEachTier()
    {
        // Act
        var freeLimits = _service.GetLimitsForTier(UserTier.Free);
        var proLimits = _service.GetLimitsForTier(UserTier.Pro);
        var vipLimits = _service.GetLimitsForTier(UserTier.Vip);

        // Assert
        freeLimits.MaxActiveHunts.Should().Be(AppConstants.Plans.Free.MaxActiveHunts);
        freeLimits.AllowAdvancedCriteria.Should().BeFalse();

        proLimits.MaxActiveHunts.Should().Be(AppConstants.Plans.Pro.MaxActiveHunts);
        proLimits.AllowAdvancedCriteria.Should().BeTrue();

        vipLimits.MaxActiveHunts.Should().Be(AppConstants.Plans.Vip.MaxActiveHunts);
        vipLimits.AllowAdvancedCriteria.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateHuntCreationAsync_ShouldThrowPlanQuotaExceededException_WhenFreeUserReachesLimit()
    {
        // Arrange
        var user = new User { Id = "user-free", Tier = UserTier.Free };
        _mockUserRepo.Setup(r => r.GetByIdAsync("user-free", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockHuntRepo.Setup(r => r.GetActiveCountByUserIdAsync("user-free", It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateHuntRequestCommand(
            UserId: "user-free",
            Url: "https://biletix.com/event",
            TargetDate: DateTime.UtcNow.AddDays(5)
        );

        // Act
        var act = async () => await _service.ValidateHuntCreationAsync("user-free", command);

        // Assert
        var ex = await act.Should().ThrowAsync<PlanQuotaExceededException>();
        ex.Which.MaxAllowed.Should().Be(1);
        ex.Which.Tier.Should().Be(UserTier.Free.ToString());
    }

    [Fact]
    public async Task ValidateHuntCreationAsync_ShouldThrowFeatureGateException_WhenFreeUserUsesMaxPrice()
    {
        // Arrange
        var user = new User { Id = "user-free", Tier = UserTier.Free };
        _mockUserRepo.Setup(r => r.GetByIdAsync("user-free", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockHuntRepo.Setup(r => r.GetActiveCountByUserIdAsync("user-free", It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var command = new CreateHuntRequestCommand(
            UserId: "user-free",
            Url: "https://biletix.com/event",
            TargetDate: DateTime.UtcNow.AddDays(5),
            MaxPrice: 750
        );

        // Act
        var act = async () => await _service.ValidateHuntCreationAsync("user-free", command);

        // Assert
        var ex = await act.Should().ThrowAsync<FeatureGateException>();
        ex.Which.RequiredTier.Should().Be(UserTier.Pro.ToString());
    }

    [Fact]
    public async Task ValidateHuntCreationAsync_ShouldThrowFeatureGateException_WhenFreeUserUsesPreferredCategory()
    {
        // Arrange
        var user = new User { Id = "user-free", Tier = UserTier.Free };
        _mockUserRepo.Setup(r => r.GetByIdAsync("user-free", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockHuntRepo.Setup(r => r.GetActiveCountByUserIdAsync("user-free", It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var command = new CreateHuntRequestCommand(
            UserId: "user-free",
            Url: "https://biletix.com/event",
            TargetDate: DateTime.UtcNow.AddDays(5),
            PreferredCategory: "VIP"
        );

        // Act
        var act = async () => await _service.ValidateHuntCreationAsync("user-free", command);

        // Assert
        var ex = await act.Should().ThrowAsync<FeatureGateException>();
        ex.Which.RequiredTier.Should().Be(UserTier.Pro.ToString());
    }

    [Fact]
    public async Task ValidateHuntCreationAsync_ShouldSucceed_WhenProUserWithinLimitAndUsesAdvancedCriteria()
    {
        // Arrange
        var user = new User { Id = "user-pro", Tier = UserTier.Pro };
        _mockUserRepo.Setup(r => r.GetByIdAsync("user-pro", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockHuntRepo.Setup(r => r.GetActiveCountByUserIdAsync("user-pro", It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var command = new CreateHuntRequestCommand(
            UserId: "user-pro",
            Url: "https://biletix.com/event",
            TargetDate: DateTime.UtcNow.AddDays(5),
            MaxPrice: 1500,
            PreferredCategory: "Kategori 1"
        );

        // Act & Assert
        var act = async () => await _service.ValidateHuntCreationAsync("user-pro", command);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateHuntCreationAsync_ShouldThrowPlanQuotaExceededException_WhenProUserReachesFiveHunts()
    {
        // Arrange
        var user = new User { Id = "user-pro", Tier = UserTier.Pro };
        _mockUserRepo.Setup(r => r.GetByIdAsync("user-pro", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockHuntRepo.Setup(r => r.GetActiveCountByUserIdAsync("user-pro", It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var command = new CreateHuntRequestCommand(
            UserId: "user-pro",
            Url: "https://biletix.com/event",
            TargetDate: DateTime.UtcNow.AddDays(5)
        );

        // Act
        var act = async () => await _service.ValidateHuntCreationAsync("user-pro", command);

        // Assert
        var ex = await act.Should().ThrowAsync<PlanQuotaExceededException>();
        ex.Which.MaxAllowed.Should().Be(5);
        ex.Which.Tier.Should().Be(UserTier.Pro.ToString());
    }

    [Fact]
    public async Task ValidateHuntCreationAsync_ShouldSucceed_WhenVipUserHasTenHunts()
    {
        // Arrange
        var user = new User { Id = "user-vip", Tier = UserTier.Vip };
        _mockUserRepo.Setup(r => r.GetByIdAsync("user-vip", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockHuntRepo.Setup(r => r.GetActiveCountByUserIdAsync("user-vip", It.IsAny<CancellationToken>())).ReturnsAsync(10);

        var command = new CreateHuntRequestCommand(
            UserId: "user-vip",
            Url: "https://biletix.com/event",
            TargetDate: DateTime.UtcNow.AddDays(5),
            MaxPrice: 5000,
            PreferredCategory: "VIP Lounge"
        );

        // Act & Assert
        var act = async () => await _service.ValidateHuntCreationAsync("user-vip", command);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateHuntCreationAsync_ShouldThrowFeatureGateException_WhenFreeUserRequestsAutoReservation()
    {
        // Arrange
        var user = new User { Id = "user-free", Tier = UserTier.Free };
        _mockUserRepo.Setup(r => r.GetByIdAsync("user-free", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockHuntRepo.Setup(r => r.GetActiveCountByUserIdAsync("user-free", It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var command = new CreateHuntRequestCommand(
            UserId: "user-free",
            Url: "https://biletix.com/event",
            TargetDate: DateTime.UtcNow.AddDays(5),
            AutoReserveEnabled: true
        );

        // Act
        var act = async () => await _service.ValidateHuntCreationAsync("user-free", command);

        // Assert
        var ex = await act.Should().ThrowAsync<FeatureGateException>();
        ex.Which.RequiredTier.Should().Be(UserTier.Vip.ToString());
    }

    [Fact]
    public async Task ValidateHuntCreationAsync_ShouldThrowFeatureGateException_WhenProUserRequestsAutoReservation()
    {
        // Arrange
        var user = new User { Id = "user-pro", Tier = UserTier.Pro };
        _mockUserRepo.Setup(r => r.GetByIdAsync("user-pro", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockHuntRepo.Setup(r => r.GetActiveCountByUserIdAsync("user-pro", It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var command = new CreateHuntRequestCommand(
            UserId: "user-pro",
            Url: "https://biletix.com/event",
            TargetDate: DateTime.UtcNow.AddDays(5),
            AutoReserveEnabled: true
        );

        // Act
        var act = async () => await _service.ValidateHuntCreationAsync("user-pro", command);

        // Assert
        var ex = await act.Should().ThrowAsync<FeatureGateException>();
        ex.Which.RequiredTier.Should().Be(UserTier.Vip.ToString());
    }

    [Fact]
    public async Task ValidateHuntCreationAsync_ShouldSucceed_WhenVipUserRequestsAutoReservation()
    {
        // Arrange
        var user = new User { Id = "user-vip", Tier = UserTier.Vip };
        _mockUserRepo.Setup(r => r.GetByIdAsync("user-vip", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockHuntRepo.Setup(r => r.GetActiveCountByUserIdAsync("user-vip", It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var command = new CreateHuntRequestCommand(
            UserId: "user-vip",
            Url: "https://biletix.com/event",
            TargetDate: DateTime.UtcNow.AddDays(5),
            AutoReserveEnabled: true
        );

        // Act & Assert
        var act = async () => await _service.ValidateHuntCreationAsync("user-vip", command);
        await act.Should().NotThrowAsync();
    }
}
