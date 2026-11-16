using SeatHunter.Domain.Entities;

namespace SeatHunter.Application.Interfaces;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<Subscription?> GetLatestByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task AddSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken = default);
    Task UpdateSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken = default);
    Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);
    Task UpdateTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);
    Task<PaymentTransaction?> GetTransactionByExternalSessionIdAsync(string externalSessionId, CancellationToken cancellationToken = default);
    Task<PaymentTransaction?> GetTransactionByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<List<PaymentTransaction>> GetUserTransactionsAsync(string userId, int take = 20, CancellationToken cancellationToken = default);
    Task<List<Subscription>> GetDueExpiredSubscriptionsAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
