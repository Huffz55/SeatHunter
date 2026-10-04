using System.Collections.Generic;
using System.Threading;
using SeatHunter.Application.DTOs;

namespace SeatHunter.Application.Interfaces;

public interface IHuntManagerService
{
    void AddHunt(ActiveHuntDto huntDto, CancellationTokenSource cts);
    void CancelHunt(string huntId);
    IEnumerable<ActiveHuntDto> GetActiveHunts();
    void RemoveHunt(string huntId);
}
