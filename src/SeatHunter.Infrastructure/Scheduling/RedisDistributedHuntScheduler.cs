using Microsoft.Extensions.Logging;
using SeatHunter.Application.Interfaces;
using StackExchange.Redis;

namespace SeatHunter.Infrastructure.Scheduling;

public class RedisDistributedHuntScheduler : IDistributedHuntScheduler
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisDistributedHuntScheduler> _logger;
    private const string SchedulerKey = "seathunter:scheduler:due_hunts";

    // Atomik olarak süresi dolmuş görevleri çeken ve aynı anda kuyruktan çıkaran Lua betiği
    private const string PopDueHuntsScript = @"
        local key = KEYS[1]
        local maxScore = tonumber(ARGV[1])
        local limit = tonumber(ARGV[2])
        local elements = redis.call('ZRANGEBYSCORE', key, '-inf', maxScore, 'LIMIT', 0, limit)
        if #elements > 0 then
            redis.call('ZREM', key, unpack(elements))
        end
        return elements
    ";

    public RedisDistributedHuntScheduler(
        IConnectionMultiplexer redis, 
        ILogger<RedisDistributedHuntScheduler> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task ScheduleHuntAsync(Guid huntRequestId, DateTime nextScanTime, CancellationToken cancellationToken = default)
    {
        if (!_redis.IsConnected)
        {
            _logger.LogWarning("Redis bağlantısı kapalı, zamanlama Redis'e kaydedilemedi: {HuntId}", huntRequestId);
            return;
        }

        try
        {
            var db = _redis.GetDatabase();
            var score = new DateTimeOffset(nextScanTime.ToUniversalTime()).ToUnixTimeMilliseconds();
            await db.SortedSetAddAsync(SchedulerKey, huntRequestId.ToString(), score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis ZSET'e görev zamanlanırken hata oluştu: {HuntId}", huntRequestId);
        }
    }

    public async Task RemoveHuntAsync(Guid huntRequestId, CancellationToken cancellationToken = default)
    {
        if (!_redis.IsConnected)
        {
            return;
        }

        try
        {
            var db = _redis.GetDatabase();
            await db.SortedSetRemoveAsync(SchedulerKey, huntRequestId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis ZSET'ten görev silinirken hata oluştu: {HuntId}", huntRequestId);
        }
    }

    public async Task<List<Guid>> FetchDueHuntsAsync(DateTime now, int maxBatchSize = 50, CancellationToken cancellationToken = default)
    {
        if (!_redis.IsConnected)
        {
            return new List<Guid>();
        }

        try
        {
            var db = _redis.GetDatabase();
            var maxScore = new DateTimeOffset(now.ToUniversalTime()).ToUnixTimeMilliseconds();

            var redisResult = await db.ScriptEvaluateAsync(
                PopDueHuntsScript, 
                new RedisKey[] { (RedisKey)SchedulerKey }, 
                new RedisValue[] { maxScore, maxBatchSize });

            if (redisResult.IsNull)
            {
                return new List<Guid>();
            }

            var items = (RedisValue[]?)redisResult;
            if (items == null || items.Length == 0)
            {
                return new List<Guid>();
            }

            var list = new List<Guid>(items.Length);
            foreach (var item in items)
            {
                if (item.HasValue && Guid.TryParse(item.ToString(), out var guid))
                {
                    list.Add(guid);
                }
            }
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis ZSET üzerinden zamanı gelen görevler çekilirken hata oluştu.");
            return new List<Guid>();
        }
    }

    public async Task<long> GetScheduledCountAsync(CancellationToken cancellationToken = default)
    {
        if (!_redis.IsConnected)
        {
            return 0;
        }

        try
        {
            var db = _redis.GetDatabase();
            return await db.SortedSetLengthAsync(SchedulerKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis ZSET toplam eleman sayısı sorgulanırken hata oluştu.");
            return 0;
        }
    }
}
