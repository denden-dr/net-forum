using NetForum.Data.Entities;

namespace NetForum.Data.Repositories;

/// <summary>
/// Defines persistence contract queries and mutations specifically for user notifications.
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    /// Inserts a new notification record into the database.
    /// </summary>
    Task<Notification> CreateNotificationAsync(Notification notification);

    /// <summary>
    /// Fetches recent notifications for a user, ordered by newest first, limited to a specified count.
    /// </summary>
    Task<List<Notification>> GetNotificationsForUserAsync(Guid userId, int limit = 20);

    /// <summary>
    /// Returns the number of unread notifications for a user.
    /// </summary>
    Task<int> GetUnreadNotificationCountAsync(Guid userId);

    /// <summary>
    /// Marks a single notification as read in the database.
    /// </summary>
    Task MarkNotificationAsReadAsync(Guid notificationId);

    /// <summary>
    /// Marks all unread notifications for a user as read.
    /// </summary>
    Task MarkAllNotificationsAsReadForUserAsync(Guid userId);

    /// <summary>
    /// Resolves a user by their case-insensitive username.
    /// </summary>
    Task<User?> GetUserByUsernameAsync(string username);

    /// <summary>
    /// Resolves multiple users by their case-insensitive usernames in a single query to prevent N+1 query loops.
    /// </summary>
    Task<List<User>> GetUsersByUsernamesAsync(IEnumerable<string> usernames);
}
