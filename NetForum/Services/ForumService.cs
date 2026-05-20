using NetForum.Data.Entities;
using NetForum.Data.Repositories;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Services;

public class ForumService(
    IForumRepository repository,
    IServiceScopeFactory scopeFactory,
    ILogger<ForumService> logger,
    ICurrentUserService currentUserService,
    IStorageService storageService) : IForumService
{
    public Task<List<Category>> GetCategoriesAsync() => repository.GetCategoriesAsync();

    public Task<Category?> GetCategoryBySlugAsync(string slug) => repository.GetCategoryBySlugAsync(slug);

    public Task<List<Thread>> GetThreadsAsync(int? categoryId = null, string? searchQuery = null) =>
        repository.GetThreadsAsync(categoryId, searchQuery);

    public async Task<Thread?> GetThreadByIdAsync(Guid threadId, bool incrementViewCount = false)
    {
        var thread = await repository.GetThreadByIdAsync(threadId);
        if (thread != null && incrementViewCount)
        {
            thread.Views++;
            await repository.UpdateThreadAsync(thread);
        }

        return thread;
    }

    public Task<List<Post>> GetPostsForThreadAsync(Guid threadId) => repository.GetPostsForThreadAsync(threadId);

    private async Task<User> EnsureEmailConfirmedAsync()
    {
        if (!currentUserService.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var userId = currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID is missing.");
        var user = await repository.GetUserByIdAsync(userId) ??
                   throw new UnauthorizedAccessException("User profile not found.");

        if (!user.EmailConfirmed)
        {
            throw new UnauthorizedAccessException("You must verify your email address before performing this action.");
        }

        return user;
    }

    public async Task<Thread> CreateThreadAsync(int categoryId, string title, string content)
    {
        var user = await EnsureEmailConfirmedAsync();

        var thread = new Thread
        {
            CategoryId = categoryId,
            Title = title.Trim(),
            Content = content.Trim(),
            AuthorId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            Upvotes = 1 // Starts with 1 initial self-upvote
        };

        var created = await repository.CreateThreadAsync(thread);
        created.Author = user;

        // Capture closure variables safely
        var contentCopy = content;
        var threadIdCopy = created.Id;
        var senderCopy = user;

        // Non-blocking fire-and-forget mentions processing
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notificationService.ParseAndCreateMentionsAsync(contentCopy, threadIdCopy, null, senderCopy);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background mention processing failed for thread {ThreadId}", threadIdCopy);
            }
        });

        return created;
    }

    public async Task UpvoteThreadAsync(Guid threadId)
    {
        await EnsureEmailConfirmedAsync();

        var thread = await repository.GetThreadByIdAsync(threadId);
        if (thread != null)
        {
            thread.Upvotes++;
            await repository.UpdateThreadAsync(thread);
        }
    }

    public async Task<Post> CreatePostAsync(Guid threadId, string content, Guid? replyToPostId = null)
    {
        var user = await EnsureEmailConfirmedAsync();

        var post = new Post
        {
            ThreadId = threadId,
            Content = content.Trim(),
            AuthorId = user.Id,
            ReplyToPostId = replyToPostId,
            CreatedAt = DateTimeOffset.UtcNow,
            Upvotes = 0
        };

        var created = await repository.CreatePostAsync(post);
        created.Author = user;

        // Capture closure variables safely
        var contentCopy = content;
        var threadIdCopy = threadId;
        var postIdCopy = created.Id;
        var replyToIdCopy = replyToPostId;
        var senderCopy = user;

        // Fire-and-forget notifications processing
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var bgRepo = scope.ServiceProvider.GetRequiredService<IForumRepository>();

                var notifiedUserIds = new List<Guid>();

                // 1. Thread reply notification
                var threadEntity = await bgRepo.GetThreadByIdAsync(threadIdCopy);
                if (threadEntity != null && threadEntity.AuthorId != senderCopy.Id)
                {
                    notifiedUserIds.Add(threadEntity.AuthorId);
                    var snippet = contentCopy.Length > 60 ? contentCopy[..60] + "..." : contentCopy;
                    var preview = $"{senderCopy.Username} replied to your thread: \"{snippet}\"";
                    await notificationService.CreateNotificationAsync(threadEntity.AuthorId, senderCopy.Id,
                        threadIdCopy, postIdCopy,
                        preview, NotificationType.ThreadReply);
                }

                // 2. Quote notification
                if (replyToIdCopy.HasValue)
                {
                    var parentPost = await bgRepo.GetPostByIdAsync(replyToIdCopy.Value);
                    if (parentPost != null && parentPost.AuthorId != senderCopy.Id &&
                        (threadEntity == null || parentPost.AuthorId != threadEntity.AuthorId))
                    {
                        notifiedUserIds.Add(parentPost.AuthorId);
                        var snippet = contentCopy.Length > 60 ? contentCopy[..60] + "..." : contentCopy;
                        var preview = $"{senderCopy.Username} quoted your reply: \"{snippet}\"";
                        await notificationService.CreateNotificationAsync(parentPost.AuthorId, senderCopy.Id,
                            threadIdCopy,
                            postIdCopy, preview, NotificationType.QuoteReply);
                    }
                }

                // 3. Mentions processing
                await notificationService.ParseAndCreateMentionsAsync(contentCopy, threadIdCopy, postIdCopy,
                    senderCopy, notifiedUserIds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background notification processing failed for post {PostId}", postIdCopy);
            }
        });

        return created;
    }

    public async Task UpvotePostAsync(Guid postId)
    {
        await EnsureEmailConfirmedAsync();

        var post = await repository.GetPostByIdAsync(postId);
        if (post != null)
        {
            post.Upvotes++;
            await repository.UpdatePostAsync(post);
        }
    }

    public async Task<bool> IsCurrentEmailConfirmedAsync()
    {
        if (!currentUserService.IsAuthenticated || !currentUserService.UserId.HasValue)
        {
            return false;
        }

        var user = await repository.GetUserByIdAsync(currentUserService.UserId.Value);
        return user?.EmailConfirmed ?? false;
    }

    private static readonly HashSet<string> AllowedAvatarContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp"
    };

    private const long MaxAvatarSizeBytes = 5 * 1024 * 1024; // 5 MB

    public Task<User?> GetUserProfileAsync(string username) =>
        repository.GetUserByUsernameAsync(username);

    public Task<List<Thread>> GetRecentThreadsByUserAsync(Guid userId, int skip = 0, int count = 10) =>
        repository.GetRecentThreadsByUserAsync(userId, skip, count);

    public Task<List<Post>> GetRecentPostsByUserAsync(Guid userId, int skip = 0, int count = 10) =>
        repository.GetRecentPostsByUserAsync(userId, skip, count);

    public async Task UpdateUserProfileAsync(string? bio, Stream? avatarStream, string? avatarFileName,
        string? avatarContentType)
    {
        var user = await EnsureEmailConfirmedAsync();
        string? oldAvatarUrl = null;

        // Server-side avatar validation
        if (avatarStream != null)
        {
            long length;
            Stream uploadStream = avatarStream;

            try
            {
                length = avatarStream.Length;
            }
            catch (NotSupportedException)
            {
                var ms = new MemoryStream();
                await avatarStream.CopyToAsync(ms);
                ms.Position = 0;
                uploadStream = ms;
                length = ms.Length;
            }

            if (length > MaxAvatarSizeBytes)
                throw new InvalidOperationException("Avatar file size must not exceed 5 MB.");

            if (string.IsNullOrEmpty(avatarContentType) || !AllowedAvatarContentTypes.Contains(avatarContentType))
                throw new InvalidOperationException("Avatar must be PNG, JPEG, or WebP.");

            oldAvatarUrl = user.AvatarUrl;

            var sanitizedFileName = Path.GetFileName(avatarFileName ?? "avatar.jpg");
            user.AvatarUrl = await storageService.UploadAvatarAsync(uploadStream, sanitizedFileName, avatarContentType);
        }

        user.Bio = bio;
        await repository.UpdateUserAsync(user);

        // Delete old avatar from storage only after successful db write
        if (!string.IsNullOrEmpty(oldAvatarUrl))
        {
            await storageService.DeleteObjectByUrlAsync(oldAvatarUrl);
        }
    }
}
