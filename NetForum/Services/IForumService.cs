using NetForum.Data.Entities;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Services;

/// <summary>
/// Provides unified business operations and validations for categories, threads, and posts.
/// </summary>
public interface IForumService
{
    /// <summary>
    /// Retrieves all categories ordered ascending by display order.
    /// </summary>
    Task<List<Category>> GetCategoriesAsync();

    /// <summary>
    /// Retrieves a category by its lowercase unique URL slug (trimmed, case-insensitive).
    /// </summary>
    Task<Category?> GetCategoryBySlugAsync(string slug);

    /// <summary>
    /// Retrieves threads filtered optionally by category and keyword search query (ordered by newest first).
    /// </summary>
    Task<List<Thread>> GetThreadsAsync(int? categoryId = null, string? searchQuery = null);

    /// <summary>
    /// Retrieves a thread by its unique identifier, optionally incrementing its view count.
    /// </summary>
    Task<Thread?> GetThreadByIdAsync(Guid threadId, bool incrementViewCount = false);

    /// <summary>
    /// Creates and persists a new thread under a category with string sanitization and auto self-upvoting.
    /// </summary>
    Task<Thread> CreateThreadAsync(int categoryId, string title, string content);

    /// <summary>
    /// Increments a thread's upvote count by 1.
    /// </summary>
    Task UpvoteThreadAsync(Guid threadId);

    /// <summary>
    /// Retrieves replies for a thread, including self-referencing quote links (chronologically ordered).
    /// </summary>
    Task<List<Post>> GetPostsForThreadAsync(Guid threadId);

    /// <summary>
    /// Creates a reply post for a thread with string sanitization and optional citation of a parent post.
    /// </summary>
    Task<Post> CreatePostAsync(Guid threadId, string content, Guid? replyToPostId = null);

    /// <summary>
    /// Increments a post's upvote count by 1.
    /// </summary>
    Task UpvotePostAsync(Guid postId);

    /// <summary>
    /// Checks if the current logged-in user's email is confirmed.
    /// </summary>
    Task<bool> IsCurrentEmailConfirmedAsync();
}
