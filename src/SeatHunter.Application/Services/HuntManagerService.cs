using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SeatHunter.Application.DTOs;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Application.Services;

public class HuntManagerService : IHuntManagerService
{
    private class ActiveHuntEntry
    {
        public ActiveHuntDto Dto { get; set; } = null!;
        public CancellationTokenSource Cts { get; set; } = null!;
    }

    private readonly ConcurrentDictionary<string, ActiveHuntEntry> _activeHunts = new();

    public void AddHunt(ActiveHuntDto huntDto, CancellationTokenSource cts)
    {
        _activeHunts.TryAdd(huntDto.Id, new ActiveHuntEntry { Dto = huntDto, Cts = cts });
    }

        public void CancelHunt(string huntId)
    {
        if (_activeHunts.TryRemove(huntId, out var entry))
        {
            if (!entry.Cts.IsCancellationRequested)
            {
                entry.Cts.Cancel();
            }
            entry.Cts.Dispose();
        }
    }

    public IEnumerable<ActiveHuntDto> GetActiveHunts()
    {
        return _activeHunts.Values.Select(v => v.Dto).ToList();
    }

    public void RemoveHunt(string huntId)
    {
        if (_activeHunts.TryRemove(huntId, out var entry))
        {
            entry.Cts.Dispose();
        }
    }
}
