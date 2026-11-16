using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SeatHunter.Application.Interfaces;
using SeatHunter.Application.Services;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Application.Tests.Services;

public class BillingServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ISubscriptionRepository> _subRepo = new();
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<INotificationRepository> _notifRepo = new();
    private readonly BillingService _sut;

    public BillingServiceTests()
    {
        _gateway.SetupGet(g => g.ProviderName).Returns("Simulated");
        _sut = new BillingService(
            _userRepo.Object,
            _subRepo.Object,
            _gateway.Object,
            Mock.Of<ILogger<BillingService>>(),
            _notifRepo.Object);
    }

    [Fact]
    public async Task StartCheckoutAsync_ShouldRejectFreeTier_AndReturnExistingSessionForSameIdempotencyKey()
    {
        var user = new User { Id = "u-1", Email = "u1@example.com", Tier = UserTier.Free, IsActive = true };
        _userRepo.Setup(r => r.GetByIdAsync("u-1", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.StartCheckoutAsync("u-1", new StartCheckoutCommand(UserTier.Free, BillingCycle.Monthly)));

        var existingTx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = "u-1",
            TargetTier = UserTier.Pro,
            Cycle = BillingCycle.Monthly,
            Amount = 149m,
            Currency = "TRY",
            Provider = "Simulated",
            ExternalSessionId = "sim_cs_existing",
            IdempotencyKey = "idem-1"
        };
        _subRepo.Setup(r => r.GetTransactionByIdempotencyKeyAsync("idem-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTx);

        var res = await _sut.StartCheckoutAsync("u-1", new StartCheckoutCommand(UserTier.Pro, BillingCycle.Monthly, "idem-1"));

        res.ExternalSessionId.Should().Be("sim_cs_existing");
        _gateway.Verify(g => g.CreateCheckoutSessionAsync(It.IsAny<CreateCheckoutSessionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhookAsync_ShouldRejectInvalidSignature_AndBeIdempotentOnSucceededReplay()
    {
        var user = new User { Id = "u-2", Email = "u2@example.com", Tier = UserTier.Free, IsActive = true };
        var tx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = "u-2",
            TargetTier = UserTier.Pro,
            Cycle = BillingCycle.Monthly,
            Amount = 149m,
            Currency = "TRY",
            Status = PaymentTransactionStatus.Pending,
            Provider = "Simulated",
            ExternalSessionId = "sim_cs_2",
            IdempotencyKey = "idem-2"
        };

        _gateway.Setup(g => g.VerifyWebhookSignature("raw", "bad-sig")).Returns(false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.ProcessWebhookAsync(new BillingWebhookEvent("payment.succeeded", "sim_cs_2"), "raw", "bad-sig"));

        _gateway.Setup(g => g.VerifyWebhookSignature("raw", "good-sig")).Returns(true);
        _subRepo.Setup(r => r.GetTransactionByExternalSessionIdAsync("sim_cs_2", It.IsAny<CancellationToken>())).ReturnsAsync(tx);
        _userRepo.Setup(r => r.GetByIdAsync("u-2", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var first = await _sut.ProcessWebhookAsync(new BillingWebhookEvent("payment.succeeded", "sim_cs_2"), "raw", "good-sig");
        first.Succeeded.Should().BeTrue();
        first.EffectiveTier.Should().Be("Pro");
        tx.Status.Should().Be(PaymentTransactionStatus.Succeeded);
        user.Tier.Should().Be(UserTier.Pro);
        user.SubscriptionExpiresAtUtc.Should().NotBeNull();

        // İkinci replay'de yeni abonelik açılmamalı
        var second = await _sut.ProcessWebhookAsync(new BillingWebhookEvent("payment.succeeded", "sim_cs_2"), "raw", "good-sig");
        second.Succeeded.Should().BeTrue();
        _subRepo.Verify(r => r.AddSubscriptionAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAndExpire_ShouldPreserveTierUntilPeriodEnd_ThenDowngradeToFree()
    {
        var user = new User
        {
            Id = "u-3",
            Email = "u3@example.com",
            Tier = UserTier.Vip,
            SubscriptionExpiresAtUtc = DateTime.UtcNow.AddDays(10)
        };
        var activeSub = new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = "u-3",
            PlanTier = UserTier.Vip,
            Status = SubscriptionStatus.Active,
            CurrentPeriodEndUtc = DateTime.UtcNow.AddDays(10)
        };

        _userRepo.Setup(r => r.GetByIdAsync("u-3", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _subRepo.Setup(r => r.GetActiveByUserIdAsync("u-3", It.IsAny<CancellationToken>())).ReturnsAsync(activeSub);

        var cancelResult = await _sut.CancelAutoRenewAsync("u-3");
        cancelResult.Succeeded.Should().BeTrue();
        cancelResult.EffectiveTier.Should().Be("Vip");
        activeSub.CancelAtPeriodEnd.Should().BeTrue();

        // Süresi dolmuş abonelik simülasyonu
        activeSub.CurrentPeriodEndUtc = DateTime.UtcNow.AddMinutes(-5);
        user.SubscriptionExpiresAtUtc = activeSub.CurrentPeriodEndUtc;
        user.GetEffectiveTier().Should().Be(UserTier.Free);

        _subRepo.Setup(r => r.GetDueExpiredSubscriptionsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Subscription> { activeSub });

        var expiredCount = await _sut.ExpireDueSubscriptionsAsync();
        expiredCount.Should().Be(1);
        activeSub.Status.Should().Be(SubscriptionStatus.Expired);
        user.Tier.Should().Be(UserTier.Free);
        user.SubscriptionExpiresAtUtc.Should().BeNull();
    }
}
