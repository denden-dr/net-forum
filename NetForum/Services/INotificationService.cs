using NetForum.Data.Entities;

namespace NetForum.Services;

/// <summary>
/// Provides unified business operations and validations for user notifications and mentions.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Retrieves recent notifications for a user, ordered by newest first, limited to a specified count.
    /// </summary>
    Task<List<Notification>> GetNotificationsForUserAsync(Guid userId, int limit = 20);

    /// <summary>
    /// Retrieves the number of unread notifications for a user.
    /// </summary>
    Task<int> GetUnreadNotificationCountAsync(Guid userId);

    /// <summary>
    /// Marks a single notification as read in the database asynchronously (non-blocking).
    /// </summary>
    Task MarkNotificationAsReadAsync(Guid notificationId);

    /// <summary>
    /// Marks all unread notifications for a user as read asynchronously (non-blocking).
    /// </summary>
    Task MarkAllNotificationsAsReadForUserAsync(Guid userId);

    /// <summary>
    /// Parses text content for @mentions and triggers database notification records asynchronously.
    /// </summary>
    Task ParseAndCreateMentionsAsync(string content, Guid threadId, Guid? postId, User sender, IEnumerable<Guid>? excludedUserIds = null);

    /// <summary>
    /// Manually triggers and writes a new notification to the database.
    /// </summary>
    Task CreateNotificationAsync(Guid recipientId, Guid senderId, Guid threadId, Guid? postId, string contentPreview, NotificationType type = NotificationType.Mention);
}
