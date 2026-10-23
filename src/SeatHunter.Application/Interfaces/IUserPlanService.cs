using SeatHunter.Domain.Enums;
using SeatHunter.Application.Features.HuntRequests.Commands.CreateHuntRequest;

namespace SeatHunter.Application.Interfaces;

public record UserPlanLimits(
    UserTier Tier,
    int MaxActiveHunts,
    int MinScanIntervalMinutes,
    bool AllowAdvancedCriteria,
    bool AllowAutoReservation = false
);

public interface IUserPlanService
{
    UserPlanLimits GetLimitsForTier(UserTier tier);
    Task ValidateHuntCreationAsync(string userId, CreateHuntRequestCommand command, CancellationToken cancellationToken = default);
}
