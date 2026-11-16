using SeatHunter.Domain.Constants;
using SeatHunter.Domain.Enums;

namespace SeatHunter.Application.Common;

public record PlanDefinition(
    UserTier Tier,
    string Name,
    decimal MonthlyPriceTry,
    decimal YearlyPriceTry,
    string Currency,
    int MaxActiveHunts,
    int MinScanIntervalMinutes,
    bool AllowAdvancedCriteria,
    bool AllowAutoReservation
);

/// <summary>
/// Tekil kaynak olarak SaaS plan fiyatlandırmasını ve kota özelliklerini tanımlar.
/// </summary>
public static class PlanCatalog
{
    public const string DefaultCurrency = "TRY";

    private static readonly IReadOnlyDictionary<UserTier, PlanDefinition> Plans =
        new Dictionary<UserTier, PlanDefinition>
        {
            [UserTier.Free] = new(
                Tier: UserTier.Free,
                Name: "Free",
                MonthlyPriceTry: 0m,
                YearlyPriceTry: 0m,
                Currency: DefaultCurrency,
                MaxActiveHunts: AppConstants.Plans.Free.MaxActiveHunts,
                MinScanIntervalMinutes: AppConstants.Plans.Free.MinScanIntervalMinutes,
                AllowAdvancedCriteria: AppConstants.Plans.Free.AllowAdvancedCriteria,
                AllowAutoReservation: AppConstants.Plans.Free.AllowAutoReservation),

            [UserTier.Pro] = new(
                Tier: UserTier.Pro,
                Name: "Pro",
                MonthlyPriceTry: 149m,
                YearlyPriceTry: 1490m,
                Currency: DefaultCurrency,
                MaxActiveHunts: AppConstants.Plans.Pro.MaxActiveHunts,
                MinScanIntervalMinutes: AppConstants.Plans.Pro.MinScanIntervalMinutes,
                AllowAdvancedCriteria: AppConstants.Plans.Pro.AllowAdvancedCriteria,
                AllowAutoReservation: AppConstants.Plans.Pro.AllowAutoReservation),

            [UserTier.Vip] = new(
                Tier: UserTier.Vip,
                Name: "VIP",
                MonthlyPriceTry: 399m,
                YearlyPriceTry: 3990m,
                Currency: DefaultCurrency,
                MaxActiveHunts: AppConstants.Plans.Vip.MaxActiveHunts,
                MinScanIntervalMinutes: AppConstants.Plans.Vip.MinScanIntervalMinutes,
                AllowAdvancedCriteria: AppConstants.Plans.Vip.AllowAdvancedCriteria,
                AllowAutoReservation: AppConstants.Plans.Vip.AllowAutoReservation)
        };

    public static IReadOnlyList<PlanDefinition> GetAll() =>
        new[] { Plans[UserTier.Free], Plans[UserTier.Pro], Plans[UserTier.Vip] };

    public static PlanDefinition Get(UserTier tier) => Plans[tier];

    public static decimal GetPrice(UserTier tier, BillingCycle cycle)
    {
        var def = Get(tier);
        return cycle == BillingCycle.Yearly ? def.YearlyPriceTry : def.MonthlyPriceTry;
    }

    public static DateTime CalculatePeriodEnd(DateTime periodStartUtc, BillingCycle cycle)
    {
        return cycle == BillingCycle.Yearly
            ? periodStartUtc.AddYears(1)
            : periodStartUtc.AddMonths(1);
    }
}
