namespace SeatHunter.Application.Interfaces;

/// <summary>
/// Tek bir bilet tarama talebinin sağlayıcı kontrolü, fiyat/kategori değerlendirmesi ve bildirim sürecini yürütür.
/// </summary>
public interface IHuntExecutionService
{
    Task ExecuteHuntCheckAsync(
        Guid requestId,
        bool rescheduleIfNotFound = true,
        CancellationToken cancellationToken = default);
}
