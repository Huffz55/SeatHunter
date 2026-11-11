using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SeatHunter.Domain.Entities;
using SeatHunter.Infrastructure.Persistence;
using SeatHunter.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SeatHunter.Infrastructure.Tests;

public class NotificationRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly SeatHunterDbContext _context;
    private readonly NotificationRepository _repository;

    public NotificationRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        var options = new DbContextOptionsBuilder<SeatHunterDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new SeatHunterDbContext(options);
        _repository = new NotificationRepository(_context);
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_And_GetByUserIdAsync_ShouldReturnUsersNotificationsOrderedByNewestFirst()
    {
        var older = new UserNotification
        {
            UserId = "user-1",
            Title = "Eski Bildirim",
            Message = "İlk mesaj",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var newer = new UserNotification
        {
            UserId = "user-1",
            Title = "Yeni Bildirim",
            Message = "İkinci mesaj",
            CreatedAt = DateTime.UtcNow
        };
        var otherUser = new UserNotification
        {
            UserId = "user-2",
            Title = "Diğer Kullanıcı",
            Message = "Görünmemeli",
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(older);
        await _repository.AddAsync(newer);
        await _repository.AddAsync(otherUser);

        var results = await _repository.GetByUserIdAsync("user-1", 50);

        results.Should().HaveCount(2);
        results[0].Id.Should().Be(newer.Id);
        results[1].Id.Should().Be(older.Id);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ShouldCountOnlyUnreadNotificationsForTargetUser()
    {
        await _repository.AddAsync(new UserNotification { UserId = "user-1", Title = "1", IsRead = false });
        await _repository.AddAsync(new UserNotification { UserId = "user-1", Title = "2", IsRead = false });
        await _repository.AddAsync(new UserNotification { UserId = "user-1", Title = "3", IsRead = true });
        await _repository.AddAsync(new UserNotification { UserId = "user-2", Title = "4", IsRead = false });

        var count = await _repository.GetUnreadCountAsync("user-1");

        count.Should().Be(2);
    }

    [Fact]
    public async Task MarkAsReadAsync_ShouldEnforceIdorProtection_WhenOwnedByAnotherUser()
    {
        var notification = new UserNotification
        {
            UserId = "owner-user",
            Title = "Özel Bildirim",
            IsRead = false
        };
        await _repository.AddAsync(notification);

        var stolenAttempt = await _repository.MarkAsReadAsync(notification.Id, "attacker-user");
        var ownerUnread = await _repository.GetUnreadCountAsync("owner-user");

        stolenAttempt.Should().BeFalse();
        ownerUnread.Should().Be(1);

        var ownerAttempt = await _repository.MarkAsReadAsync(notification.Id, "owner-user");
        var ownerUnreadAfter = await _repository.GetUnreadCountAsync("owner-user");

        ownerAttempt.Should().BeTrue();
        ownerUnreadAfter.Should().Be(0);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_ShouldMarkOnlyTargetUsersUnreadNotifications()
    {
        await _repository.AddAsync(new UserNotification { UserId = "user-a", Title = "A1", IsRead = false });
        await _repository.AddAsync(new UserNotification { UserId = "user-a", Title = "A2", IsRead = false });
        await _repository.AddAsync(new UserNotification { UserId = "user-b", Title = "B1", IsRead = false });

        var updatedCount = await _repository.MarkAllAsReadAsync("user-a");

        updatedCount.Should().Be(2);
        (await _repository.GetUnreadCountAsync("user-a")).Should().Be(0);
        (await _repository.GetUnreadCountAsync("user-b")).Should().Be(1);
    }

    [Fact]
    public async Task DeleteReadAsync_ShouldRemoveOnlyReadNotificationsForTargetUser()
    {
        var unreadA = new UserNotification { UserId = "user-a", Title = "Okunmamış", IsRead = false };
        var readA = new UserNotification { UserId = "user-a", Title = "Okunmuş", IsRead = true };
        var readB = new UserNotification { UserId = "user-b", Title = "Diğer Okunmuş", IsRead = true };

        await _repository.AddAsync(unreadA);
        await _repository.AddAsync(readA);
        await _repository.AddAsync(readB);

        var deletedCount = await _repository.DeleteReadAsync("user-a");

        deletedCount.Should().Be(1);
        var remainingA = await _repository.GetByUserIdAsync("user-a");
        remainingA.Should().ContainSingle().Which.Id.Should().Be(unreadA.Id);
        (await _repository.GetByUserIdAsync("user-b")).Should().ContainSingle().Which.Id.Should().Be(readB.Id);
    }
}
