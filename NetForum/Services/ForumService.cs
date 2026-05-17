using NetForum.Data.Entities;
using NetForum.Data.Repositories;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Services;

public class ForumService : IForumService
{
    private readonly IForumRepository _repository;

    public ForumService(IForumRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<Category>> GetCategoriesAsync()
    {
        return await _repository.GetCategoriesAsync();
    }

    public async Task<Category?> GetCategoryBySlugAsync(string slug)
    {
        return await _repository.GetCategoryBySlugAsync(slug);
    }

    public async Task<List<Thread>> GetThreadsAsync(int? categoryId = null, string? searchQuery = null)
    {
        return await _repository.GetThreadsAsync(categoryId, searchQuery);
    }

    public async Task<Thread?> GetThreadByIdAsync(Guid threadId, bool incrementViewCount = false)
    {
        var thread = await _repository.GetThreadByIdAsync(threadId);

        if (thread != null && incrementViewCount)
        {
            thread.Views++;
            await _repository.UpdateThreadAsync(thread);
        }

        return thread;
    }

    public async Task<Thread> CreateThreadAsync(int categoryId, string title, string content, string authorName)
    {
        var thread = new Thread
        {
            CategoryId = categoryId,
            Title = title.Trim(),
            Content = content.Trim(),
            AuthorName = string.IsNullOrWhiteSpace(authorName) ? "Anonymous" : authorName.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            Upvotes = 1 // Starts with 1 initial self-upvote
        };

        return await _repository.CreateThreadAsync(thread);
    }

    public async Task UpvoteThreadAsync(Guid threadId)
    {
        var thread = await _repository.GetThreadByIdAsync(threadId);
        if (thread != null)
        {
            thread.Upvotes++;
            await _repository.UpdateThreadAsync(thread);
        }
    }

    public async Task<List<Post>> GetPostsForThreadAsync(Guid threadId)
    {
        return await _repository.GetPostsForThreadAsync(threadId);
    }

    public async Task<Post> CreatePostAsync(Guid threadId, string content, string authorName, Guid? replyToPostId = null)
    {
        var post = new Post
        {
            ThreadId = threadId,
            Content = content.Trim(),
            AuthorName = string.IsNullOrWhiteSpace(authorName) ? "Anonymous" : authorName.Trim(),
            ReplyToPostId = replyToPostId,
            CreatedAt = DateTimeOffset.UtcNow,
            Upvotes = 0
        };

        return await _repository.CreatePostAsync(post);
    }

    public async Task UpvotePostAsync(Guid postId)
    {
        var post = await _repository.GetPostByIdAsync(postId);
        if (post != null)
        {
            post.Upvotes++;
            await _repository.UpdatePostAsync(post);
        }
    }
}
