using NetForum.Data.Entities;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Data.Repositories;

/// <summary>
/// Defines persistence contract queries and mutations for categories, threads, posts, and users.
/// </summary>
public interface IForumRepository
{
    /// <summary>
    /// Fetches all categories ordered ascending by display order from the database.
    /// </summary>
    Task<List<Category>> GetCategoriesAsync();

    /// <summary>
    /// Fetches a category by its unique URL slug (case-insensitive check).
    /// </summary>
    Task<Category?> GetCategoryBySlugAsync(string slug);

    /// <summary>
    /// Fetches threads filtered optionally by category and keyword search query (ordered by newest first).
    /// </summary>
    Task<List<Thread>> GetThreadsAsync(int? categoryId = null, string? searchQuery = null);

    /// <summary>
    /// Fetches a single thread and its category/author details by thread GUID.
    /// </summary>
    Task<Thread?> GetThreadByIdAsync(Guid threadId);

    /// <summary>
    /// Inserts a new thread record into the database.
    /// </summary>
    Task<Thread> CreateThreadAsync(Thread thread);

    /// <summary>
    /// Persists updates to an existing thread entity.
    /// </summary>
    Task UpdateThreadAsync(Thread thread);

    /// <summary>
    /// Fetches all replies for a thread, including self-referencing quote links and authors (chronologically ordered).
    /// </summary>
    Task<List<Post>> GetPostsForThreadAsync(Guid threadId);

    /// <summary>
    /// Inserts a new reply post record into the database.
    /// </summary>
    Task<Post> CreatePostAsync(Post post);

    /// <summary>
    /// Persists updates to an existing reply post entity.
    /// </summary>
    Task UpdatePostAsync(Post post);

    /// <summary>
    /// Fetches a single post entity by post GUID.
    /// </summary>
    Task<Post?> GetPostByIdAsync(Guid postId);

    /// <summary>
    /// Fetches a single user profile by GUID.
    /// </summary>
    Task<User?> GetUserByIdAsync(Guid userId);

    /// <summary>
    /// Inserts a new user record into the database.
    /// </summary>
    Task<User> CreateUserAsync(User user);

    /// <summary>
    /// Fetches a user by username (case-insensitive).
    /// </summary>
    Task<User?> GetUserByUsernameAsync(string username);

    /// <summary>
    /// Fetches the most recent threads authored by a user.
    /// </summary>
    Task<List<Thread>> GetRecentThreadsByUserAsync(Guid userId, int count = 10);

    /// <summary>
    /// Fetches the most recent posts authored by a user.
    /// </summary>
    Task<List<Post>> GetRecentPostsByUserAsync(Guid userId, int count = 10);

    /// <summary>
    /// Persists updates to an existing user entity (bio, avatar, etc).
    /// </summary>
    Task UpdateUserAsync(User user);
}
