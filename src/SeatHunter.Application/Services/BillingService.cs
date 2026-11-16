using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SeatHunter.Application.Common;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Constants;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Application.Services;

/// <summary>
/// SaaS abonelik yaşam döngüsü, idempotent checkout/webhook işleme ve otomatik plan düşürme servisi.
/// </summary>
public class BillingService : IBillingService
{
    private static readonly ActivitySource ActivitySource = new(AppConstants.Metrics.MeterName);
    private readonly IUserRepository _userRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly INotificationRepository? _notificationRepository;
    private readonly IScraperMetrics? _metrics;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        IUserRepository userRepository,
        ISubscriptionRepository subscriptionRepository,
        IPaymentGateway paymentGateway,
        ILogger<BillingService> logger,
        INotificationRepository? notificationRepository = null,
        IScraperMetrics? metrics = null)
    {
        _userRepository = userRepository;
        _subscriptionRepository = subscriptionRepository;
        _paymentGateway = paymentGateway;
        _logger = logger;
        _notificationRepository = notificationRepository;
        _metrics = metrics;
    }

    public IReadOnlyList<PlanDefinition> GetPlansCatalog() => PlanCatalog.GetAll();

    public async Task<UserBillingStatusDto> GetUserBillingStatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");

        var activeSub = await _subscriptionRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        var transactions = await _subscriptionRepository.GetUserTransactionsAsync(userId, 20, cancellationToken);
        var now = DateTime.UtcNow;

        return new UserBillingStatusDto(
            EffectiveTier: user.GetEffectiveTier(now).ToString(),
            SubscriptionExpiresAtUtc: user.SubscriptionExpiresAtUtc,
            ActiveSubscription: activeSub == null ? null : new ActiveSubscriptionDto(
                Id: activeSub.Id,
                PlanTier: activeSub.PlanTier.ToString(),
                Cycle: activeSub.Cycle.ToString(),
                Status: activeSub.Status.ToString(),
                Provider: activeSub.Provider,
                CurrentPeriodStartUtc: activeSub.CurrentPeriodStartUtc,
                CurrentPeriodEndUtc: activeSub.CurrentPeriodEndUtc,
                CancelAtPeriodEnd: activeSub.CancelAtPeriodEnd,
                CancelledAtUtc: activeSub.CancelledAtUtc),
            Transactions: transactions.Select(tx => new BillingTransactionDto(
                Id: tx.Id,
                TargetTier: tx.TargetTier.ToString(),
                Cycle: tx.Cycle.ToString(),
                Amount: tx.Amount,
                Currency: tx.Currency,
                Status: tx.Status.ToString(),
                Provider: tx.Provider,
                ExternalSessionId: tx.ExternalSessionId,
                FailureReason: tx.FailureReason,
                CreatedAtUtc: tx.CreatedAtUtc,
                CompletedAtUtc: tx.CompletedAtUtc)).ToList()
        );
    }

    public async Task<CheckoutSessionResult> StartCheckoutAsync(
        string userId,
        StartCheckoutCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.TargetTier == UserTier.Free)
            throw new InvalidOperationException("Ücretsiz plan için ödeme oturumu başlatılamaz. Mevcut aboneliği iptal etmek için iptal ucunu kullanın.");

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Askıya alınmış hesaplar için abonelik başlatılamaz.");

        var effectiveTier = user.GetEffectiveTier();
        if (effectiveTier == UserTier.Vip && command.TargetTier == UserTier.Pro)
            throw new InvalidOperationException("Aktif VIP plandan Pro plana geçiş için mevcut dönemin bitmesini veya iptalini bekleyin.");

        var idempotencyKey = !string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? command.IdempotencyKey.Trim()
            : Guid.NewGuid().ToString("N");

        var existingTx = await _subscriptionRepository.GetTransactionByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existingTx != null && existingTx.UserId == userId)
        {
            return new CheckoutSessionResult(
                TransactionId: existingTx.Id,
                ExternalSessionId: existingTx.ExternalSessionId,
                Provider: existingTx.Provider,
                CheckoutUrl: $"/api/billing/sandbox/confirm/{existingTx.ExternalSessionId}",
                TargetTier: existingTx.TargetTier.ToString(),
                Cycle: existingTx.Cycle.ToString(),
                Amount: existingTx.Amount,
                Currency: existingTx.Currency
            );
        }

        var amount = PlanCatalog.GetPrice(command.TargetTier, command.Cycle);
        var session = await _paymentGateway.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionRequest(
                UserId: user.Id,
                UserEmail: user.Email,
                TargetTier: command.TargetTier,
                Cycle: command.Cycle,
                Amount: amount,
                Currency: PlanCatalog.DefaultCurrency,
                IdempotencyKey: idempotencyKey,
                SuccessRedirectUrl: command.SuccessRedirectUrl,
                CancelRedirectUrl: command.CancelRedirectUrl),
            cancellationToken);

        var transaction = new PaymentTransaction
        {
            UserId = user.Id,
            TargetTier = command.TargetTier,
            Cycle = command.Cycle,
            Amount = amount,
            Currency = PlanCatalog.DefaultCurrency,
            Status = PaymentTransactionStatus.Pending,
            Provider = session.Provider,
            ExternalSessionId = session.ExternalSessionId,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _subscriptionRepository.AddTransactionAsync(transaction, cancellationToken);
        _metrics?.RecordCheckoutStarted(command.TargetTier.ToString(), command.Cycle.ToString());

        return new CheckoutSessionResult(
            TransactionId: transaction.Id,
            ExternalSessionId: session.ExternalSessionId,
            Provider: session.Provider,
            CheckoutUrl: session.CheckoutUrl,
            TargetTier: command.TargetTier.ToString(),
            Cycle: command.Cycle.ToString(),
            Amount: amount,
            Currency: PlanCatalog.DefaultCurrency
        );
    }

    public async Task<SubscriptionMutationResult> ConfirmSandboxCheckoutAsync(
        string userId,
        string externalSessionId,
        CancellationToken cancellationToken = default)
    {
        var tx = await _subscriptionRepository.GetTransactionByExternalSessionIdAsync(externalSessionId, cancellationToken)
            ?? throw new KeyNotFoundException("Ödeme oturumu bulunamadı.");

        if (!string.Equals(tx.UserId, userId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Bu ödeme oturumunu onaylama yetkiniz yok.");

        return await CompletePaymentSucceededAsync(tx, externalSubscriptionId: $"sub_sim_{tx.Id:N}", cancellationToken);
    }

    public async Task<SubscriptionMutationResult> ProcessWebhookAsync(
        BillingWebhookEvent webhookEvent,
        string rawPayload,
        string? signatureHeader,
        CancellationToken cancellationToken = default)
    {
        if (!_paymentGateway.VerifyWebhookSignature(rawPayload, signatureHeader))
        {
            _logger.LogWarning("Geçersiz billing webhook imzası reddedildi. SessionId: {SessionId}", webhookEvent.ExternalSessionId);
            _metrics?.RecordWebhookProcessed(webhookEvent.EventType, "invalid_signature");
            throw new UnauthorizedAccessException("Geçersiz webhook imzası.");
        }

        using var activity = ActivitySource.StartActivity("Billing.ProcessWebhook");
        activity?.SetTag("billing.event_type", webhookEvent.EventType);
        activity?.SetTag("billing.session_id", webhookEvent.ExternalSessionId);

        var tx = await _subscriptionRepository.GetTransactionByExternalSessionIdAsync(webhookEvent.ExternalSessionId, cancellationToken);
        if (tx == null)
        {
            _metrics?.RecordWebhookProcessed(webhookEvent.EventType, "not_found");
            return new SubscriptionMutationResult(false, UserTier.Free.ToString(), null, "İşlem kaydı bulunamadı.");
        }

        var normalizedType = webhookEvent.EventType.Trim().ToLowerInvariant();
        if (normalizedType is "payment.succeeded" or "checkout.session.completed")
        {
            var result = await CompletePaymentSucceededAsync(tx, webhookEvent.ExternalSubscriptionId, cancellationToken);
            _metrics?.RecordWebhookProcessed(normalizedType, "succeeded");
            return result;
        }

        if (normalizedType is "payment.failed" or "invoice.payment_failed")
        {
            if (tx.Status == PaymentTransactionStatus.Pending)
            {
                tx.Status = PaymentTransactionStatus.Failed;
                tx.FailureReason = webhookEvent.FailureReason ?? "Ödeme sağlayıcısı tarafından reddedildi.";
                tx.CompletedAtUtc = DateTime.UtcNow;
                await _subscriptionRepository.UpdateTransactionAsync(tx, cancellationToken);
            }

            _metrics?.RecordWebhookProcessed(normalizedType, "failed");
            var user = await _userRepository.GetByIdAsync(tx.UserId, cancellationToken);
            return new SubscriptionMutationResult(
                Succeeded: false,
                EffectiveTier: (user?.GetEffectiveTier() ?? UserTier.Free).ToString(),
                SubscriptionExpiresAtUtc: user?.SubscriptionExpiresAtUtc,
                Message: tx.FailureReason ?? "Ödeme başarısız oldu."
            );
        }

        return new SubscriptionMutationResult(true, tx.TargetTier.ToString(), null, "Desteklenmeyen olay türü yok sayıldı.");
    }

    public async Task<SubscriptionMutationResult> CancelAutoRenewAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");

        var activeSub = await _subscriptionRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        if (activeSub == null)
        {
            return new SubscriptionMutationResult(
                Succeeded: false,
                EffectiveTier: user.GetEffectiveTier().ToString(),
                SubscriptionExpiresAtUtc: user.SubscriptionExpiresAtUtc,
                Message: "Aktif ücretli aboneliğiniz bulunmuyor."
            );
        }

        if (!activeSub.CancelAtPeriodEnd)
        {
            activeSub.CancelAtPeriodEnd = true;
            activeSub.CancelledAtUtc = DateTime.UtcNow;
            activeSub.UpdatedAtUtc = DateTime.UtcNow;
            await _subscriptionRepository.UpdateSubscriptionAsync(activeSub, cancellationToken);
        }

        return new SubscriptionMutationResult(
            Succeeded: true,
            EffectiveTier: user.GetEffectiveTier().ToString(),
            SubscriptionExpiresAtUtc: activeSub.CurrentPeriodEndUtc,
            Message: $"Aboneliğiniz dönem sonunda ({activeSub.CurrentPeriodEndUtc:dd.MM.yyyy}) sonlanacak şekilde iptal edildi."
        );
    }

    public async Task<int> ExpireDueSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dueSubs = await _subscriptionRepository.GetDueExpiredSubscriptionsAsync(now, cancellationToken);
        int processed = 0;

        foreach (var sub in dueSubs)
        {
            sub.Status = SubscriptionStatus.Expired;
            sub.UpdatedAtUtc = now;
            await _subscriptionRepository.UpdateSubscriptionAsync(sub, cancellationToken);

            var user = await _userRepository.GetByIdAsync(sub.UserId, cancellationToken);
            if (user != null && user.Tier != UserTier.Free)
            {
                user.Tier = UserTier.Free;
                user.SubscriptionExpiresAtUtc = null;
                await _userRepository.UpdateAsync(user, cancellationToken);

                await TryRecordNotificationAsync(
                    user.Id,
                    "Abonelik Süresi Doldu",
                    $"{sub.PlanTier} abonelik döneminiz sona erdiği için hesabınız Free plana geçirildi.",
                    "warning",
                    cancellationToken);
            }

            processed++;
        }

        _metrics?.RecordSubscriptionsExpired(processed);
        return processed;
    }

    private async Task<SubscriptionMutationResult> CompletePaymentSucceededAsync(
        PaymentTransaction tx,
        string? externalSubscriptionId,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(tx.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Ödeme sahibi kullanıcı bulunamadı.");

        // Idempotent tekrar koruması: zaten tamamlandıysa dönemi ikinci kez uzatma
        if (tx.Status == PaymentTransactionStatus.Succeeded)
        {
            return new SubscriptionMutationResult(
                Succeeded: true,
                EffectiveTier: user.GetEffectiveTier().ToString(),
                SubscriptionExpiresAtUtc: user.SubscriptionExpiresAtUtc,
                Message: "Bu ödeme oturumu zaten işlenmiş."
            );
        }

        var now = DateTime.UtcNow;
        var existingActive = await _subscriptionRepository.GetActiveByUserIdAsync(user.Id, cancellationToken);
        var periodStart = (existingActive != null && existingActive.PlanTier == tx.TargetTier && existingActive.CurrentPeriodEndUtc > now)
            ? existingActive.CurrentPeriodEndUtc
            : now;

        if (existingActive != null)
        {
            existingActive.Status = SubscriptionStatus.Cancelled;
            existingActive.CancelledAtUtc ??= now;
            existingActive.UpdatedAtUtc = now;
            await _subscriptionRepository.UpdateSubscriptionAsync(existingActive, cancellationToken);
        }

        var periodEnd = PlanCatalog.CalculatePeriodEnd(periodStart, tx.Cycle);
        var newSubscription = new Subscription
        {
            UserId = user.Id,
            PlanTier = tx.TargetTier,
            Cycle = tx.Cycle,
            Status = SubscriptionStatus.Active,
            Provider = tx.Provider,
            ExternalSubscriptionId = externalSubscriptionId,
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = periodEnd,
            CancelAtPeriodEnd = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _subscriptionRepository.AddSubscriptionAsync(newSubscription, cancellationToken);

        tx.Status = PaymentTransactionStatus.Succeeded;
        tx.SubscriptionId = newSubscription.Id;
        tx.CompletedAtUtc = now;
        await _subscriptionRepository.UpdateTransactionAsync(tx, cancellationToken);

        user.Tier = tx.TargetTier;
        user.SubscriptionExpiresAtUtc = periodEnd;
        await _userRepository.UpdateAsync(user, cancellationToken);

        await TryRecordNotificationAsync(
            user.Id,
            $"{tx.TargetTier} Planı Aktifleştirildi",
            $"{tx.TargetTier} aboneliğiniz ({tx.Amount:0.##} {tx.Currency}) başarıyla tanımlandı. Geçerlilik: {periodEnd:dd.MM.yyyy}.",
            "success",
            cancellationToken);

        _logger.LogInformation("Kullanıcı {UserId} için {Tier} ({Cycle}) aboneliği aktifleştirildi. Bitiş: {ExpiresAt}",
            user.Id, tx.TargetTier, tx.Cycle, periodEnd);

        return new SubscriptionMutationResult(
            Succeeded: true,
            EffectiveTier: user.Tier.ToString(),
            SubscriptionExpiresAtUtc: periodEnd,
            Message: $"{tx.TargetTier} planınız başarıyla aktifleştirildi."
        );
    }

    private async Task TryRecordNotificationAsync(
        string userId,
        string title,
        string message,
        string type,
        CancellationToken cancellationToken)
    {
        if (_notificationRepository == null) return;
        try
        {
            await _notificationRepository.AddAsync(new UserNotification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Abonelik bildirimi DB'ye kaydedilemedi (UserId: {UserId}).", userId);
        }
    }
}
