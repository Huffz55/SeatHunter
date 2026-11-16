using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Constants;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Enums;
using SeatHunter.Domain.Interfaces;

namespace SeatHunter.Application.Services;

/// <summary>
/// Worker ve kuyruk tüketicileri için ortak, tekil (idempotent) bilet tarama yürütücüsü.
/// </summary>
public class HuntExecutionService : IHuntExecutionService
{
    private static readonly ActivitySource ActivitySource = new(AppConstants.Metrics.MeterName);
    private readonly IHuntRequestRepository _huntRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITicketProviderFactory _providerFactory;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IDistributedHuntScheduler _scheduler;
    private readonly ILogger<HuntExecutionService> _logger;
    private readonly ITicketPriceHistoryService? _priceHistoryService;
    private readonly ITicketReservationEngine? _reservationEngine;
    private readonly IScraperMetrics? _scraperMetrics;

    public HuntExecutionService(
        IHuntRequestRepository huntRepository,
        IUserRepository userRepository,
        ITicketProviderFactory providerFactory,
        INotificationDispatcher notificationDispatcher,
        IDistributedHuntScheduler scheduler,
        ILogger<HuntExecutionService> logger,
        ITicketPriceHistoryService? priceHistoryService = null,
        ITicketReservationEngine? reservationEngine = null,
        IScraperMetrics? scraperMetrics = null)
    {
        _huntRepository = huntRepository;
        _userRepository = userRepository;
        _providerFactory = providerFactory;
        _notificationDispatcher = notificationDispatcher;
        _scheduler = scheduler;
        _logger = logger;
        _priceHistoryService = priceHistoryService;
        _reservationEngine = reservationEngine;
        _scraperMetrics = scraperMetrics;
    }

    public async Task ExecuteHuntCheckAsync(
        Guid requestId,
        bool rescheduleIfNotFound = true,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("HuntExecution.Inspect");
        activity?.SetTag("hunt.request_id", requestId);

        var hunt = await _huntRepository.GetByIdAsync(requestId, cancellationToken);
        if (hunt == null || hunt.Status != RequestStatus.Active)
        {
            await _scheduler.RemoveHuntAsync(requestId, cancellationToken);
            return;
        }

        activity?.SetTag("hunt.provider", hunt.ProviderName);
        var ticketProvider = _providerFactory.GetProvider(hunt.RouteOrEventId);
        _logger.LogInformation("{Provider} üzerinden {Route} için tarama yapılıyor...", hunt.ProviderName, hunt.RouteOrEventId);

        var sw = Stopwatch.StartNew();
        TicketInspectionResult inspection;
        try
        {
            inspection = await ticketProvider.InspectAvailabilityAsync(
                hunt.RouteOrEventId,
                hunt.PreferredCategory,
                cancellationToken);
            sw.Stop();
            _scraperMetrics?.RecordScrapeAttempt(
                hunt.ProviderName,
                inspection.IsAvailable ? "found" : "not_found",
                sw.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _scraperMetrics?.RecordScrapeAttempt(hunt.ProviderName, "canceled", sw.Elapsed.TotalMilliseconds);
            throw;
        }
        catch (Exception)
        {
            sw.Stop();
            _scraperMetrics?.RecordScrapeAttempt(hunt.ProviderName, "error", sw.Elapsed.TotalMilliseconds);
            if (rescheduleIfNotFound)
            {
                await RescheduleOnErrorAsync(hunt);
            }
            throw;
        }

        bool categoryMatches = string.IsNullOrWhiteSpace(hunt.PreferredCategory) || inspection.CategoryMatched;
        decimal observedPrice = inspection.Price ?? hunt.MaxPrice ?? 750m;
        PriceEvaluationResult? priceEvaluation = null;

        if (inspection.IsAvailable && categoryMatches && _priceHistoryService != null)
        {
            priceEvaluation = await _priceHistoryService.RecordPriceObservationAsync(
                routeOrEventId: hunt.RouteOrEventId,
                providerName: hunt.ProviderName,
                eventName: hunt.EventName,
                observedPrice: observedPrice,
                categoryName: hunt.PreferredCategory,
                userMaxPrice: hunt.MaxPrice,
                cancellationToken: cancellationToken);
        }

        bool priceMatches = !hunt.MaxPrice.HasValue || (inspection.Price.HasValue && inspection.Price.Value <= hunt.MaxPrice.Value);
        bool isFound = inspection.IsAvailable && categoryMatches && priceMatches;

        if (isFound)
        {
            await CompleteAndNotifyAsync(hunt, observedPrice, priceEvaluation, cancellationToken);
            return;
        }

        if (inspection.IsAvailable && !categoryMatches)
        {
            _logger.LogInformation(
                "ℹ️ Bilet satışta fakat tercih edilen kategori ({Category}) bulunamadı veya tükendi. Request ID: {RequestId}",
                hunt.PreferredCategory, hunt.Id);
        }
        else if (inspection.IsAvailable && !priceMatches)
        {
            _logger.LogInformation(
                "ℹ️ Bilet satışta fakat fiyat kriteri sağlanmadı (Bulunan: {ParsedPrice}, Max: {MaxPrice}). Request ID: {RequestId}",
                inspection.Price, hunt.MaxPrice, hunt.Id);
        }

        if (hunt.TargetDate < DateTime.UtcNow)
        {
            hunt.Status = RequestStatus.Expired;
            _logger.LogInformation("Etkinlik tarihi geçmiş olduğu için tarama sonlandırıldı (Expired) - Request ID: {RequestId}", hunt.Id);
            await _huntRepository.UpdateAsync(hunt, cancellationToken);
            await _scheduler.RemoveHuntAsync(hunt.Id, cancellationToken);
            return;
        }

        if (rescheduleIfNotFound)
        {
            await RescheduleNextScanAsync(hunt, cancellationToken);
        }
    }

    private async Task CompleteAndNotifyAsync(
        HuntRequest hunt,
        decimal observedPrice,
        PriceEvaluationResult? priceEvaluation,
        CancellationToken cancellationToken)
    {
        hunt.Status = RequestStatus.Completed;
        hunt.CompletedAt = DateTime.UtcNow;

        var user = await _userRepository.GetByIdAsync(hunt.UserId, cancellationToken);

        bool isReserved = false;
        string? cartUrl = null;
        DateTime? reservedUntil = null;

        if (hunt.AutoReserveEnabled && user?.GetEffectiveTier() == UserTier.Vip && _reservationEngine != null)
        {
            _logger.LogInformation("⚡ VIP Otomatik Sepete Ekleme başlatılıyor - Request ID: {RequestId}", hunt.Id);
            var reserveResult = await _reservationEngine.AttemptReserveAsync(hunt, cancellationToken);
            if (reserveResult.IsSuccess)
            {
                isReserved = true;
                cartUrl = reserveResult.CartUrl;
                reservedUntil = reserveResult.ExpiresAt;
                hunt.ReservationCartUrl = cartUrl;
                hunt.ReservedUntil = reservedUntil;
                _logger.LogInformation("✅ VIP Koltuk başarıyla rezerve edildi! Sepet linki: {CartUrl}", cartUrl);
            }
        }

        await _huntRepository.UpdateAsync(hunt, cancellationToken);
        await _scheduler.RemoveHuntAsync(hunt.Id, cancellationToken);

        var notification = new TicketFoundNotification(
            RequestId: hunt.Id,
            UserId: hunt.UserId,
            UserEmail: user?.Email,
            ProviderName: hunt.ProviderName,
            RouteOrEventId: hunt.RouteOrEventId,
            EventName: hunt.EventName,
            Language: hunt.Language,
            IsAutoReserved: isReserved,
            ReservationCartUrl: cartUrl,
            ReservedUntil: reservedUntil,
            ObservedPrice: observedPrice,
            IsPriceDrop: priceEvaluation?.IsPriceDrop ?? false,
            PriceDropPercentage: priceEvaluation?.DropPercentage,
            LowestHistoricalPrice: priceEvaluation?.Summary?.MinPrice,
            NotifyEmail: user?.NotifyEmail ?? true,
            NotifyTelegram: user?.NotifyTelegram ?? true,
            NotifyDiscord: user?.NotifyDiscord ?? true,
            TelegramChatId: user?.TelegramChatId,
            DiscordWebhookUrl: user?.DiscordWebhookUrl);

        await _notificationDispatcher.DispatchTicketFoundAsync(notification, cancellationToken);
    }

    private async Task RescheduleNextScanAsync(HuntRequest hunt, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(hunt.UserId, cancellationToken);
        var userTier = user?.GetEffectiveTier() ?? UserTier.Free;

        var timeUntilTarget = hunt.TargetDate - DateTime.UtcNow;
        hunt.NextScanTime = CalculateNextScanTime(timeUntilTarget, userTier);

        _logger.LogInformation(
            "Bilet bulunamadı ({RequestId}) [Plan: {Tier}]. Sonraki tarama: {NextScanTime}",
            hunt.Id, userTier, hunt.NextScanTime.ToLocalTime());

        await _huntRepository.UpdateAsync(hunt, cancellationToken);
        await _scheduler.ScheduleHuntAsync(hunt.Id, hunt.NextScanTime, cancellationToken);
    }

    private async Task RescheduleOnErrorAsync(HuntRequest hunt)
    {
        hunt.NextScanTime = DateTime.UtcNow.AddMinutes(5);
        await _huntRepository.UpdateAsync(hunt, CancellationToken.None);
        await _scheduler.ScheduleHuntAsync(hunt.Id, hunt.NextScanTime, CancellationToken.None);
    }

    private static DateTime CalculateNextScanTime(TimeSpan timeUntilTarget, UserTier userTier)
    {
        if (timeUntilTarget.TotalDays > 30)
            return DateTime.UtcNow.AddHours(6);

        if (timeUntilTarget.TotalDays >= 7)
            return DateTime.UtcNow.AddHours(1);

        if (timeUntilTarget.TotalDays >= 1)
        {
            return userTier switch
            {
                UserTier.Vip => DateTime.UtcNow.AddMinutes(2),
                UserTier.Pro => DateTime.UtcNow.AddMinutes(5),
                _ => DateTime.UtcNow.AddMinutes(15)
            };
        }

        return userTier switch
        {
            UserTier.Vip => DateTime.UtcNow.AddSeconds(15),
            UserTier.Pro => DateTime.UtcNow.AddMinutes(1),
            _ => DateTime.UtcNow.AddMinutes(5)
        };
    }
}
