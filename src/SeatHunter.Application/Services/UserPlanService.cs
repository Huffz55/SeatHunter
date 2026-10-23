using SeatHunter.Application.Features.HuntRequests.Commands.CreateHuntRequest;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Constants;
using SeatHunter.Domain.Enums;
using SeatHunter.Domain.Exceptions;

namespace SeatHunter.Application.Services;

public class UserPlanService : IUserPlanService
{
    private readonly IUserRepository _userRepository;
    private readonly IHuntRequestRepository _huntRequestRepository;

    public UserPlanService(IUserRepository userRepository, IHuntRequestRepository huntRequestRepository)
    {
        _userRepository = userRepository;
        _huntRequestRepository = huntRequestRepository;
    }

    public UserPlanLimits GetLimitsForTier(UserTier tier)
    {
        return tier switch
        {
            UserTier.Vip => new UserPlanLimits(
                Tier: UserTier.Vip,
                MaxActiveHunts: AppConstants.Plans.Vip.MaxActiveHunts,
                MinScanIntervalMinutes: AppConstants.Plans.Vip.MinScanIntervalMinutes,
                AllowAdvancedCriteria: AppConstants.Plans.Vip.AllowAdvancedCriteria,
                AllowAutoReservation: AppConstants.Plans.Vip.AllowAutoReservation
            ),
            UserTier.Pro => new UserPlanLimits(
                Tier: UserTier.Pro,
                MaxActiveHunts: AppConstants.Plans.Pro.MaxActiveHunts,
                MinScanIntervalMinutes: AppConstants.Plans.Pro.MinScanIntervalMinutes,
                AllowAdvancedCriteria: AppConstants.Plans.Pro.AllowAdvancedCriteria,
                AllowAutoReservation: AppConstants.Plans.Pro.AllowAutoReservation
            ),
            _ => new UserPlanLimits(
                Tier: UserTier.Free,
                MaxActiveHunts: AppConstants.Plans.Free.MaxActiveHunts,
                MinScanIntervalMinutes: AppConstants.Plans.Free.MinScanIntervalMinutes,
                AllowAdvancedCriteria: AppConstants.Plans.Free.AllowAdvancedCriteria,
                AllowAutoReservation: AppConstants.Plans.Free.AllowAutoReservation
            )
        };
    }

    public async Task ValidateHuntCreationAsync(string userId, CreateHuntRequestCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        var tier = user?.Tier ?? UserTier.Free;
        var limits = GetLimitsForTier(tier);

        // 1. Aktif tarama kotası kontrolü
        var currentActiveCount = await _huntRequestRepository.GetActiveCountByUserIdAsync(userId, cancellationToken);
        if (currentActiveCount >= limits.MaxActiveHunts)
        {
            throw new PlanQuotaExceededException(
                tier: tier.ToString(),
                maxAllowed: limits.MaxActiveHunts,
                message: $"Aktif tarama kotanıza ulaştınız. Mevcut planınız ({tier}) aynı anda en fazla {limits.MaxActiveHunts} aktif taramaya izin vermektedir. Kotanızı artırmak için planınızı yükseltin."
            );
        }

        // 2. Gelişmiş kriter yetkilendirmesi (Feature Gating)
        bool hasAdvancedCriteria = command.MaxPrice.HasValue || !string.IsNullOrWhiteSpace(command.PreferredCategory);
        if (hasAdvancedCriteria && !limits.AllowAdvancedCriteria)
        {
            throw new FeatureGateException(
                featureName: "Gelişmiş Fiyat ve Kategori Filtreleme",
                requiredTier: UserTier.Pro.ToString(),
                message: "Maksimum fiyat ve tercih edilen kategori filtreleri Free planda kullanılamaz. Bu özelliği kullanmak için Pro veya VIP plana geçiş yapın."
            );
        }

        // 3. Otomatik bilet sepete ekleme yetkilendirmesi (VIP Feature Gate)
        if (command.AutoReserveEnabled && !limits.AllowAutoReservation)
        {
            throw new FeatureGateException(
                featureName: "Otomatik Sepete Ekleme ve Rezervasyon (Auto-Carting)",
                requiredTier: UserTier.Vip.ToString(),
                message: "Bilet düştüğünde anında sepete ekleme ve adınıza rezerve etme (Auto-carting) özelliği yalnızca VIP abonelere özeldir."
            );
        }
    }
}
