using System.Text.RegularExpressions;
using NetForum.Data.Entities;
using NetForum.Data.Repositories;

namespace NetForum.Services;

public class NotificationService(INotificationRepository repository) : INotificationService
{
    private static readonly Regex MentionRegex = new(@"\B@([a-zA-Z0-9_\-]+)\b", RegexOptions.Compiled | RegexOptions.NonBacktracking);

    public Task<List<Notification>> GetNotificationsForUserAsync(Guid userId, int limit = 20) =>
        repository.GetNotificationsForUserAsync(userId, limit);

    public Task<int> GetUnreadNotificationCountAsync(Guid userId) =>
        repository.GetUnreadNotificationCountAsync(userId);

    public async Task MarkNotificationAsReadAsync(Guid notificationId)
    {
        await repository.MarkNotificationAsReadAsync(notificationId);
    }

    public async Task MarkAllNotificationsAsReadForUserAsync(Guid userId)
    {
        await repository.MarkAllNotificationsAsReadForUserAsync(userId);
    }

    public async Task ParseAndCreateMentionsAsync(string content, Guid threadId, Guid? postId, User sender, IEnumerable<Guid>? excludedUserIds = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var matches = MentionRegex.Matches(content);
        var usernames = matches
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(username => !string.Equals(username, sender.Username, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (usernames.Count == 0) return;

        var recipients = await repository.GetUsersByUsernamesAsync(usernames);
        var excludedSet = excludedUserIds != null ? new HashSet<Guid>(excludedUserIds) : null;
        foreach (var recipient in recipients)
        {
            if (excludedSet != null && excludedSet.Contains(recipient.Id))
            {
                continue;
            }

            var snippet = content.Length > 60 ? content[..60] + "..." : content;
            var preview = $"{sender.Username} mentioned you: \"{snippet}\"";

            await CreateNotificationAsync(recipient.Id, sender.Id, threadId, postId, preview);
        }
    }

    public async Task CreateNotificationAsync(Guid recipientId, Guid senderId, Guid threadId, Guid? postId, string contentPreview, NotificationType type = NotificationType.Mention)
    {
        var notification = new Notification
        {
            RecipientId = recipientId,
            SenderId = senderId,
            ThreadId = threadId,
            PostId = postId,
            ContentPreview = contentPreview,
            IsRead = false,
            Type = type,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await repository.CreateNotificationAsync(notification);
    }
}
