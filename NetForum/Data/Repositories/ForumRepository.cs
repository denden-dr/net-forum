using Microsoft.EntityFrameworkCore;
using NetForum.Data.Entities;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Data.Repositories;

public class ForumRepository(IDbContextFactory<AppDbContext> contextFactory) : IForumRepository
{
    public async Task<List<Category>> GetCategoriesAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Categories
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();
    }

    public async Task<Category?> GetCategoryBySlugAsync(string slug)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var normalized = slug.Trim().ToLower();
        return await context.Categories
            .FirstOrDefaultAsync(c => c.Slug == normalized);
    }

    public async Task<List<Thread>> GetThreadsAsync(int? categoryId = null, string? searchQuery = null)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var query = context.Threads
            .Include(t => t.Category)
            .Include(t => t.Author)
            .Include(t => t.Posts)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var normalized = searchQuery.Trim().ToLower();
            query = query.Where(t => 
                t.Title.ToLower().Contains(normalized) || 
                t.Content.ToLower().Contains(normalized));
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<Thread?> GetThreadByIdAsync(Guid threadId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Threads
            .Include(t => t.Category)
            .Include(t => t.Author)
            .FirstOrDefaultAsync(t => t.Id == threadId);
    }

    public async Task<Thread> CreateThreadAsync(Thread thread)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Threads.Add(thread);
        await context.SaveChangesAsync();
        return thread;
    }

    public async Task UpdateThreadAsync(Thread thread)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Threads.Update(thread);
        await context.SaveChangesAsync();
    }

    public async Task<List<Post>> GetPostsForThreadAsync(Guid threadId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Posts
            .Include(p => p.ReplyToPost)
            .Include(p => p.Author)
            .Where(p => p.ThreadId == threadId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Post> CreatePostAsync(Post post)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Posts.Add(post);
        await context.SaveChangesAsync();
        return post;
    }

    public async Task UpdatePostAsync(Post post)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Posts.Update(post);
        await context.SaveChangesAsync();
    }

    public async Task<Post?> GetPostByIdAsync(Guid postId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Posts.Include(p => p.Author).FirstOrDefaultAsync(p => p.Id == postId);
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Users.FindAsync(userId);
    }


    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var normalized = username.Trim().ToUpper();
        return await context.Users
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized);
    }

    public async Task<List<Thread>> GetRecentThreadsByUserAsync(Guid userId, int skip = 0, int count = 10)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Threads
            .Include(t => t.Category)
            .Where(t => t.AuthorId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Post>> GetRecentPostsByUserAsync(Guid userId, int skip = 0, int count = 10)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Posts
            .Include(p => p.Thread)
            .Where(p => p.AuthorId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(count)
            .ToListAsync();
    }

    public async Task UpdateUserAsync(User user)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Users.Update(user);
        await context.SaveChangesAsync();
    }
}
