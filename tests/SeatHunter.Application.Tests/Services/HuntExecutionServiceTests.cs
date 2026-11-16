using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SeatHunter.Application.Interfaces;
using SeatHunter.Application.Services;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Enums;
using SeatHunter.Domain.Interfaces;

namespace SeatHunter.Application.Tests.Services;

public class HuntExecutionServiceTests
{
    private readonly Mock<IHuntRequestRepository> _mockHuntRepo = new();
    private readonly Mock<IUserRepository> _mockUserRepo = new();
    private readonly Mock<ITicketProviderFactory> _mockFactory = new();
    private readonly Mock<ITicketProvider> _mockProvider = new();
    private readonly Mock<INotificationDispatcher> _mockDispatcher = new();
    private readonly Mock<IDistributedHuntScheduler> _mockScheduler = new();
    private readonly Mock<ITicketPriceHistoryService> _mockPriceService = new();
    private readonly Mock<ITicketReservationEngine> _mockReservation = new();
    private readonly Mock<IScraperMetrics> _mockMetrics = new();
    private readonly Mock<ILogger<HuntExecutionService>> _mockLogger = new();

    private HuntExecutionService CreateService(bool withReservation = true)
    {
        _mockProvider.Setup(p => p.ProviderName).Returns("Universal");
        _mockFactory.Setup(f => f.GetProvider(It.IsAny<string>())).Returns(_mockProvider.Object);

        return new HuntExecutionService(
            _mockHuntRepo.Object,
            _mockUserRepo.Object,
            _mockFactory.Object,
            _mockDispatcher.Object,
            _mockScheduler.Object,
            _mockLogger.Object,
            _mockPriceService.Object,
            withReservation ? _mockReservation.Object : null,
            _mockMetrics.Object);
    }

    [Theory]
    [InlineData(RequestStatus.Cancelled)]
    [InlineData(RequestStatus.Completed)]
    [InlineData(RequestStatus.Expired)]
    public async Task ExecuteHuntCheckAsync_ShouldSkipAndRemoveFromScheduler_WhenHuntIsNotActive(RequestStatus status)
    {
        var huntId = Guid.NewGuid();
        _mockHuntRepo.Setup(r => r.GetByIdAsync(huntId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HuntRequest { Id = huntId, Status = status });

        var service = CreateService();

        await service.ExecuteHuntCheckAsync(huntId, rescheduleIfNotFound: true, CancellationToken.None);

        _mockProvider.Verify(
            p => p.InspectAvailabilityAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockDispatcher.Verify(
            d => d.DispatchTicketFoundAsync(It.IsAny<TicketFoundNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockScheduler.Verify(s => s.RemoveHuntAsync(huntId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteHuntCheckAsync_ShouldNotComplete_WhenPreferredCategoryDoesNotMatch()
    {
        var hunt = new HuntRequest
        {
            Id = Guid.NewGuid(),
            UserId = "user-pro",
            ProviderName = "BiletixLive",
            RouteOrEventId = "https://biletix.com/event/1",
            TargetDate = DateTime.UtcNow.AddDays(5),
            PreferredCategory = "Saha İçi",
            Status = RequestStatus.Active
        };

        _mockHuntRepo.Setup(r => r.GetByIdAsync(hunt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(hunt);
        _mockUserRepo.Setup(r => r.GetByIdAsync("user-pro", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = "user-pro", Tier = UserTier.Pro });
        _mockProvider.Setup(p => p.InspectAvailabilityAsync(hunt.RouteOrEventId, "Saha İçi", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketInspectionResult(IsAvailable: true, Price: 900m, CategoryMatched: false));

        var service = CreateService();
        await service.ExecuteHuntCheckAsync(hunt.Id, rescheduleIfNotFound: true, CancellationToken.None);

        hunt.Status.Should().Be(RequestStatus.Active);
        _mockDispatcher.Verify(
            d => d.DispatchTicketFoundAsync(It.IsAny<TicketFoundNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockScheduler.Verify(s => s.ScheduleHuntAsync(hunt.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1500)]
    public async Task ExecuteHuntCheckAsync_ShouldNotComplete_WhenMaxPriceSetAndPriceMissingOrAboveBudget(int? parsedPriceInt)
    {
        decimal? parsedPrice = parsedPriceInt.HasValue ? parsedPriceInt.Value : null;
        var hunt = new HuntRequest
        {
            Id = Guid.NewGuid(),
            UserId = "user-pro",
            ProviderName = "Universal",
            RouteOrEventId = "https://example.com/concert",
            TargetDate = DateTime.UtcNow.AddDays(3),
            MaxPrice = 1000m,
            Status = RequestStatus.Active
        };

        _mockHuntRepo.Setup(r => r.GetByIdAsync(hunt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(hunt);
        _mockUserRepo.Setup(r => r.GetByIdAsync("user-pro", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = "user-pro", Tier = UserTier.Pro });
        _mockProvider.Setup(p => p.InspectAvailabilityAsync(hunt.RouteOrEventId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketInspectionResult(IsAvailable: true, Price: parsedPrice, CategoryMatched: true));

        var service = CreateService();
        await service.ExecuteHuntCheckAsync(hunt.Id, rescheduleIfNotFound: true, CancellationToken.None);

        hunt.Status.Should().Be(RequestStatus.Active);
        _mockDispatcher.Verify(
            d => d.DispatchTicketFoundAsync(It.IsAny<TicketFoundNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteHuntCheckAsync_WhenMatchedAndVipReserveFails_ShouldCompleteWithHonestNonReservedNotification()
    {
        var hunt = new HuntRequest
        {
            Id = Guid.NewGuid(),
            UserId = "user-vip",
            ProviderName = "Universal",
            RouteOrEventId = "https://example.com/vip-show",
            EventName = "Jazz Festival",
            Language = "en",
            TargetDate = DateTime.UtcNow.AddDays(2),
            PreferredCategory = "VIP",
            MaxPrice = 2000m,
            AutoReserveEnabled = true,
            Status = RequestStatus.Active
        };

        _mockHuntRepo.Setup(r => r.GetByIdAsync(hunt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(hunt);
        _mockUserRepo.Setup(r => r.GetByIdAsync("user-vip", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = "user-vip", Email = "vip@example.com", Tier = UserTier.Vip });
        _mockProvider.Setup(p => p.InspectAvailabilityAsync(hunt.RouteOrEventId, "VIP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketInspectionResult(IsAvailable: true, Price: 1650m, CategoryMatched: true));
        _mockReservation.Setup(r => r.AttemptReserveAsync(hunt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReservationResult(false, null, null, "Sepete ekleme butonu sayfada bulunamadı."));

        var service = CreateService();
        await service.ExecuteHuntCheckAsync(hunt.Id, rescheduleIfNotFound: false, CancellationToken.None);

        hunt.Status.Should().Be(RequestStatus.Completed);
        hunt.CompletedAt.Should().NotBeNull();
        hunt.ReservationCartUrl.Should().BeNull();

        _mockScheduler.Verify(s => s.RemoveHuntAsync(hunt.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockDispatcher.Verify(d => d.DispatchTicketFoundAsync(
            It.Is<TicketFoundNotification>(n =>
                n.RequestId == hunt.Id &&
                n.Language == "en" &&
                n.EventName == "Jazz Festival" &&
                n.IsAutoReserved == false &&
                n.ReservationCartUrl == null &&
                n.ObservedPrice == 1650m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteHuntCheckAsync_WhenNotFoundAndRescheduleDisabled_ShouldNotTouchNextScanTime()
    {
        var originalScan = DateTime.UtcNow.AddSeconds(20);
        var hunt = new HuntRequest
        {
            Id = Guid.NewGuid(),
            UserId = "user-free",
            ProviderName = "Universal",
            RouteOrEventId = "https://example.com/show",
            TargetDate = DateTime.UtcNow.AddDays(2),
            NextScanTime = originalScan,
            Status = RequestStatus.Active
        };

        _mockHuntRepo.Setup(r => r.GetByIdAsync(hunt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(hunt);
        _mockProvider.Setup(p => p.InspectAvailabilityAsync(hunt.RouteOrEventId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketInspectionResult(false, null, true));

        var service = CreateService();
        await service.ExecuteHuntCheckAsync(hunt.Id, rescheduleIfNotFound: false, CancellationToken.None);

        hunt.NextScanTime.Should().Be(originalScan);
        _mockHuntRepo.Verify(r => r.UpdateAsync(It.IsAny<HuntRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockScheduler.Verify(s => s.ScheduleHuntAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteHuntCheckAsync_WhenTargetDatePassed_ShouldMarkExpiredAndRemoveFromScheduler()
    {
        var hunt = new HuntRequest
        {
            Id = Guid.NewGuid(),
            UserId = "user-free",
            ProviderName = "Universal",
            RouteOrEventId = "https://example.com/past",
            TargetDate = DateTime.UtcNow.AddMinutes(-5),
            Status = RequestStatus.Active
        };

        _mockHuntRepo.Setup(r => r.GetByIdAsync(hunt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(hunt);
        _mockProvider.Setup(p => p.InspectAvailabilityAsync(hunt.RouteOrEventId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketInspectionResult(false, null, true));

        var service = CreateService();
        await service.ExecuteHuntCheckAsync(hunt.Id, rescheduleIfNotFound: true, CancellationToken.None);

        hunt.Status.Should().Be(RequestStatus.Expired);
        _mockHuntRepo.Verify(r => r.UpdateAsync(hunt, It.IsAny<CancellationToken>()), Times.Once);
        _mockScheduler.Verify(s => s.RemoveHuntAsync(hunt.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
