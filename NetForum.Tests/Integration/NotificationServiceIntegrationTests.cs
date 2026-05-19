using Microsoft.EntityFrameworkCore;
using NetForum.Data;
using NetForum.Data.Entities;
using NetForum.Data.Repositories;
using NetForum.Services;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Tests.Integration;

[Collection("PostgreSqlCollection")]
public class NotificationServiceIntegrationTests : IAsyncLifetime
{
    private readonly TestDbContextFactory _factory;
    private readonly NotificationService _service;

    public NotificationServiceIntegrationTests(PostgreSqlTestFixture fixture)
    {
        _factory = new TestDbContextFactory(fixture.ConnectionString);
        var repository = new NotificationRepository(_factory);
        _service = new NotificationService(repository);
    }

    public async Task InitializeAsync()
    {
        await using var context = _factory.CreateDbContext();
        // Guarantees a clean, empty state BEFORE each test runs
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Notifications\", \"Posts\", \"Threads\", \"Users\" RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<User> CreateTestUserAsync(string username)
    {
        await using var context = _factory.CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            Email = $"{username.ToLower()}@test.com",
            NormalizedEmail = $"{username.ToLower()}@test.com".ToUpperInvariant(),
            EmailConfirmed = true,
            Role = Roles.Member,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<Thread> CreateTestThreadAsync(Guid authorId)
    {
        await using var context = _factory.CreateDbContext();
        var thread = new Thread
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Title = "Test Thread",
            Content = "Test Thread Content",
            AuthorId = authorId,
            CreatedAt = DateTimeOffset.UtcNow,
            Upvotes = 1
        };
        context.Threads.Add(thread);
        await context.SaveChangesAsync();
        return thread;
    }

    [Fact]
    public async Task CreateNotificationAsync_PersistsToPostgreSqlDatabase()
    {
        // Arrange
        var sender = await CreateTestUserAsync("SenderUser");
        var recipient = await CreateTestUserAsync("RecipientUser");
        var thread = await CreateTestThreadAsync(sender.Id);

        // Act
        await _service.CreateNotificationAsync(recipient.Id, sender.Id, thread.Id, null, "Mentioned you in thread!");

        // Assert
        await using var context = _factory.CreateDbContext();
        var dbNotif = await context.Notifications.FirstOrDefaultAsync(n => n.RecipientId == recipient.Id);
        
        Assert.NotNull(dbNotif);
        Assert.Equal(sender.Id, dbNotif.SenderId);
        Assert.Equal(thread.Id, dbNotif.ThreadId);
        Assert.Equal("Mentioned you in thread!", dbNotif.ContentPreview);
        Assert.False(dbNotif.IsRead);
    }

    [Fact]
    public async Task MarkNotificationAsReadAsync_UpdatesStateInDatabase()
    {
        // Arrange
        var sender = await CreateTestUserAsync("SenderUser");
        var recipient = await CreateTestUserAsync("RecipientUser");
        var thread = await CreateTestThreadAsync(sender.Id);

        await _service.CreateNotificationAsync(recipient.Id, sender.Id, thread.Id, null, "Preview text");
        
        await using var context = _factory.CreateDbContext();
        var initialNotif = await context.Notifications.FirstAsync(n => n.RecipientId == recipient.Id);
        Assert.False(initialNotif.IsRead);

        // Act
        await _service.MarkNotificationAsReadAsync(initialNotif.Id);
        
        // Wait briefly for background fire-and-forget task
        await Task.Delay(150);

        // Assert
        await using var assertContext = _factory.CreateDbContext();
        var updatedNotif = await assertContext.Notifications.FirstAsync(n => n.Id == initialNotif.Id);
        Assert.True(updatedNotif.IsRead);
    }

    [Fact]
    public async Task GetUnreadNotificationCountAsync_ReturnsAccurateCount()
    {
        // Arrange
        var sender = await CreateTestUserAsync("SenderUser");
        var recipient = await CreateTestUserAsync("RecipientUser");
        var thread = await CreateTestThreadAsync(sender.Id);

        // Act & Assert (Empty)
        var count0 = await _service.GetUnreadNotificationCountAsync(recipient.Id);
        Assert.Equal(0, count0);

        // Seed 2 notifications
        await _service.CreateNotificationAsync(recipient.Id, sender.Id, thread.Id, null, "Preview 1");
        await _service.CreateNotificationAsync(recipient.Id, sender.Id, thread.Id, null, "Preview 2");

        var count2 = await _service.GetUnreadNotificationCountAsync(recipient.Id);
        Assert.Equal(2, count2);
    }
}
