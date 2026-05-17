using Microsoft.EntityFrameworkCore;
using NetForum.Data.Repositories;
using NetForum.Services;

namespace NetForum.Tests.Integration;

[Collection("PostgreSqlCollection")]
public class ForumServiceIntegrationTests : IAsyncLifetime
{
    private readonly TestDbContextFactory _factory;
    private readonly ForumService _service;

    public ForumServiceIntegrationTests(PostgreSqlTestFixture fixture)
    {
        _factory = new TestDbContextFactory(fixture.ConnectionString);
        var repository = new ForumRepository(_factory);
        _service = new ForumService(repository);
    }

    public async Task InitializeAsync()
    {
        await using var context = _factory.CreateDbContext();
        // Guarantees a 100% clean, empty slate BEFORE each test runs!
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Posts\", \"Threads\" RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        // No-op after test, since we clean at start
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetCategoriesAsync_WhenSeeded_ReturnsCategoriesOrderedByDisplayOrder()
    {
        // Act
        var categories = await _service.GetCategoriesAsync();

        // Assert
        Assert.NotNull(categories);
        Assert.Equal(4, categories.Count);
        Assert.Equal("General", categories[0].Name);
        Assert.Equal("Programming", categories[1].Name);
    }

    [Fact]
    public async Task GetCategoryBySlugAsync_WithDifferentCasedSlug_ReturnsCorrectCategoryCaseInsensitive()
    {
        // Act
        var programming = await _service.GetCategoryBySlugAsync("PROGRAMMING");

        // Assert
        Assert.NotNull(programming);
        Assert.Equal("Programming", programming.Name);
        Assert.Equal("programming", programming.Slug);
    }

    [Fact]
    public async Task CreateThreadAsync_WithValidData_PersistsThreadWithInitialSelfUpvote()
    {
        // Act
        var thread = await _service.CreateThreadAsync(1, "Blazor Server TDD", "Unit testing Blazor apps is easy with bUnit.", "Developer");

        // Assert
        Assert.NotNull(thread);
        Assert.NotEqual(Guid.Empty, thread.Id);
        Assert.Equal("Blazor Server TDD", thread.Title);
        Assert.Equal("Unit testing Blazor apps is easy with bUnit.", thread.Content);
        Assert.Equal("Developer", thread.AuthorName);
        Assert.Equal(1, thread.CategoryId);
        Assert.Equal(1, thread.Upvotes);
        Assert.Equal(0, thread.Views);
    }

    [Fact]
    public async Task GetThreadsAsync_WithFiltersAndSearchQuery_FiltersAndReturnsMatchingThreads()
    {
        // Create initial threads
        await _service.CreateThreadAsync(1, "General Chit Chat", "Just checking in.", "Alice");
        var t2 = await _service.CreateThreadAsync(2, "C# .NET 10 Web Development", "Discussing the latest features.", "Bob");
        var t3 = await _service.CreateThreadAsync(2, "Vite vs Webpack in ASP.NET", "Frontend comparison.", "Charlie");

        // Act & Assert 1: Get all threads (ordered by newest first)
        var allThreads = await _service.GetThreadsAsync();
        Assert.Equal(3, allThreads.Count);
        Assert.Equal(t3.Id, allThreads[0].Id);

        // Act & Assert 2: Filter by category (Programming = Id 2)
        var programmingThreads = await _service.GetThreadsAsync(categoryId: 2);
        Assert.Equal(2, programmingThreads.Count);
        Assert.All(programmingThreads, t => Assert.Equal(2, t.CategoryId));

        // Act & Assert 3: Search by keyword case-insensitive
        var searchResult = await _service.GetThreadsAsync(searchQuery: "DEVELOPMENT");
        Assert.Single(searchResult);
        Assert.Equal(t2.Id, searchResult[0].Id);
    }

    [Fact]
    public async Task GetThreadByIdAsync_WithIncrementViewsRequested_IncrementsViewsAndPersists()
    {
        var thread = await _service.CreateThreadAsync(1, "View Count Test", "Does views increment?", "Tester");

        // Act 1: Fetch without incrementing
        var fetched1 = await _service.GetThreadByIdAsync(thread.Id, incrementViewCount: false);
        Assert.NotNull(fetched1);
        Assert.Equal(0, fetched1.Views);

        // Act 2: Fetch with incrementing
        var fetched2 = await _service.GetThreadByIdAsync(thread.Id, incrementViewCount: true);
        Assert.NotNull(fetched2);
        Assert.Equal(1, fetched2.Views);

        // Act 3: Verify update is persisted
        var fetched3 = await _service.GetThreadByIdAsync(thread.Id, incrementViewCount: false);
        Assert.NotNull(fetched3);
        Assert.Equal(1, fetched3.Views);
    }

    [Fact]
    public async Task UpvoteThreadAsync_WithExistingId_IncrementsUpvotesAndPersists()
    {
        var thread = await _service.CreateThreadAsync(1, "Upvote Thread Test", "Content", "Voter");

        // Act
        await _service.UpvoteThreadAsync(thread.Id);

        // Assert
        var updated = await _service.GetThreadByIdAsync(thread.Id);
        Assert.NotNull(updated);
        Assert.Equal(2, updated.Upvotes);
    }

    [Fact]
    public async Task CreatePostAsync_WithParentQuote_PersistsWithSelfReferencingQuoteLink()
    {
        var thread = await _service.CreateThreadAsync(1, "Quote Test Thread", "Content", "Author");

        // Act 1: Create top-level reply post
        var post1 = await _service.CreatePostAsync(thread.Id, "This is the first post.", "Alice");
        Assert.NotNull(post1);
        Assert.NotEqual(Guid.Empty, post1.Id);
        Assert.Null(post1.ReplyToPostId);
        Assert.Equal(0, post1.Upvotes);

        // Act 2: Create sub-reply quoting post1
        var post2 = await _service.CreatePostAsync(thread.Id, "> Alice said: This is the first post.\n\nI agree!", "Bob", replyToPostId: post1.Id);
        Assert.NotNull(post2);
        Assert.Equal(post1.Id, post2.ReplyToPostId);
    }

    [Fact]
    public async Task GetPostsForThreadAsync_WithExistingReplies_ReturnsRepliesInChronologicalOrder()
    {
        var thread = await _service.CreateThreadAsync(1, "Chronology Thread", "Content", "Author");

        // Create posts at slightly different times
        var p1 = await _service.CreatePostAsync(thread.Id, "First reply", "Alice");
        var p2 = await _service.CreatePostAsync(thread.Id, "Second reply", "Bob");

        // Act
        var posts = await _service.GetPostsForThreadAsync(thread.Id);

        // Assert
        Assert.Equal(2, posts.Count);
        Assert.Equal(p1.Id, posts[0].Id);
        Assert.Equal(p2.Id, posts[1].Id);
    }

    [Fact]
    public async Task UpvotePostAsync_WithExistingId_IncrementsUpvotesAndPersists()
    {
        var thread = await _service.CreateThreadAsync(1, "Thread", "Content", "Author");
        var post = await _service.CreatePostAsync(thread.Id, "Reply content", "Bob");

        // Act
        await _service.UpvotePostAsync(post.Id);

        // Assert
        var posts = await _service.GetPostsForThreadAsync(thread.Id);
        Assert.Single(posts);
        Assert.Equal(1, posts[0].Upvotes);
    }
}
