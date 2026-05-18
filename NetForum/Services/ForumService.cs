using NetForum.Data.Entities;
using NetForum.Data.Repositories;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Services;

public class ForumService(IForumRepository repository, ICurrentUserService currentUserService) : IForumService
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
        var user = await repository.GetUserByIdAsync(userId) ?? throw new UnauthorizedAccessException("User profile not found.");

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
}
