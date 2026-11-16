namespace SeatHunter.Domain.Enums;

public enum BillingCycle
{
    Monthly = 0,
    Yearly = 1
}

public enum SubscriptionStatus
{
    Active = 0,
    Cancelled = 1,
    PastDue = 2,
    Expired = 3
}

public enum PaymentTransactionStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Refunded = 3
}
