using Microsoft.EntityFrameworkCore;
using NetForum.Data.Entities;

namespace NetForum.Data.Repositories;

public class NotificationRepository(IDbContextFactory<AppDbContext> contextFactory) : INotificationRepository
{
    public async Task<Notification> CreateNotificationAsync(Notification notification)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        return notification;
    }

    public async Task<List<Notification>> GetNotificationsForUserAsync(Guid userId, int limit = 20)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Notifications
            .Include(n => n.Sender)
            .Include(n => n.Thread)
            .Include(n => n.Post)
            .Where(n => n.RecipientId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetUnreadNotificationCountAsync(Guid userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Notifications
            .CountAsync(n => n.RecipientId == userId && !n.IsRead);
    }

    public async Task MarkNotificationAsReadAsync(Guid notificationId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var notification = await context.Notifications.FindAsync(notificationId);
        if (notification != null)
        {
            notification.IsRead = true;
            await context.SaveChangesAsync();
        }
    }

    public async Task MarkAllNotificationsAsReadForUserAsync(Guid userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var unread = await context.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ToListAsync();
        
        foreach (var notif in unread)
        {
            notif.IsRead = true;
        }
        await context.SaveChangesAsync();
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var normalized = username.Trim().ToUpperInvariant();
        return await context.Users
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized);
    }

    public async Task<List<User>> GetUsersByUsernamesAsync(IEnumerable<string> usernames)
    {
        if (usernames == null || !usernames.Any())
        {
            return new List<User>();
        }
        await using var context = await contextFactory.CreateDbContextAsync();
        var normalizedUsernames = usernames
            .Select(u => u.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
        return await context.Users
            .Where(u => u.NormalizedUserName != null && normalizedUsernames.Contains(u.NormalizedUserName))
            .ToListAsync();
    }
}
