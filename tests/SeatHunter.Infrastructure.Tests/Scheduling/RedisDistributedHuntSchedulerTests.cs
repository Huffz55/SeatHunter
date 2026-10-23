using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SeatHunter.Infrastructure.Scheduling;
using StackExchange.Redis;
using Xunit;

namespace SeatHunter.Infrastructure.Tests.Scheduling;

public class RedisDistributedHuntSchedulerTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<RedisDistributedHuntScheduler>> _mockLogger;
    private readonly RedisDistributedHuntScheduler _scheduler;

    public RedisDistributedHuntSchedulerTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<RedisDistributedHuntScheduler>>();

        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDatabase.Object);
        _scheduler = new RedisDistributedHuntScheduler(_mockRedis.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ScheduleHuntAsync_ShouldNotThrow_WhenRedisDisconnected()
    {
        // Arrange
        _mockRedis.Setup(r => r.IsConnected).Returns(false);

        // Act
        var act = async () => await _scheduler.ScheduleHuntAsync(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(5));

        // Assert
        await act.Should().NotThrowAsync();
        _mockDatabase.Verify(db => db.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task ScheduleHuntAsync_ShouldCallSortedSetAdd_WhenRedisConnected()
    {
        // Arrange
        _mockRedis.Setup(r => r.IsConnected).Returns(true);
        var huntId = Guid.NewGuid();
        var scanTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        await _scheduler.ScheduleHuntAsync(huntId, scanTime);

        // Assert
        _mockDatabase.Verify(db => db.SortedSetAddAsync(
            "seathunter:scheduler:due_hunts",
            huntId.ToString(),
            It.Is<double>(score => score > 0),
            SortedSetWhen.Always,
            CommandFlags.None
        ), Times.Once);
    }

    [Fact]
    public async Task RemoveHuntAsync_ShouldCallSortedSetRemove_WhenRedisConnected()
    {
        // Arrange
        _mockRedis.Setup(r => r.IsConnected).Returns(true);
        var huntId = Guid.NewGuid();

        // Act
        await _scheduler.RemoveHuntAsync(huntId);

        // Assert
        _mockDatabase.Verify(db => db.SortedSetRemoveAsync(
            "seathunter:scheduler:due_hunts",
            huntId.ToString(),
            CommandFlags.None
        ), Times.Once);
    }

    [Fact]
    public async Task FetchDueHuntsAsync_ShouldReturnEmptyList_WhenRedisDisconnected()
    {
        // Arrange
        _mockRedis.Setup(r => r.IsConnected).Returns(false);

        // Act
        var result = await _scheduler.FetchDueHuntsAsync(DateTime.UtcNow);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchDueHuntsAsync_ShouldReturnParsedGuids_WhenScriptReturnsElements()
    {
        // Arrange
        _mockRedis.Setup(r => r.IsConnected).Returns(true);
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        RedisResult redisResult = RedisResult.Create(new RedisValue[] { id1.ToString(), id2.ToString() });

        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()
        )).ReturnsAsync(redisResult);

        // Act
        var result = await _scheduler.FetchDueHuntsAsync(DateTime.UtcNow);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(id1);
        result.Should().Contain(id2);
    }

    [Fact]
    public async Task GetScheduledCountAsync_ShouldReturnCount_WhenRedisConnected()
    {
        // Arrange
        _mockRedis.Setup(r => r.IsConnected).Returns(true);
        _mockDatabase.Setup(db => db.SortedSetLengthAsync(
            "seathunter:scheduler:due_hunts",
            It.IsAny<double>(),
            It.IsAny<double>(),
            It.IsAny<Exclude>(),
            It.IsAny<CommandFlags>()
        )).ReturnsAsync(42);

        // Act
        var count = await _scheduler.GetScheduledCountAsync();

        // Assert
        count.Should().Be(42);
    }
}
