using Microsoft.EntityFrameworkCore;
using NetForum.Data;
using NetForum.Data.Entities;
using NetForum.Data.Repositories;
using NetForum.Services;

namespace NetForum.Tests.Integration;

[Collection("PostgreSqlCollection")]
public class ForumServiceIntegrationTests : IAsyncLifetime
{
    private readonly TestDbContextFactory _factory;
    private readonly DevCurrentUserService _currentUserService;
    private readonly ForumService _service;

    public ForumServiceIntegrationTests(PostgreSqlTestFixture fixture)
    {
        _factory = new TestDbContextFactory(fixture.ConnectionString);
        var repository = new ForumRepository(_factory);
        var notificationRepository = new NotificationRepository(_factory);
        var notificationService = new NotificationService(notificationRepository);
        _currentUserService = new DevCurrentUserService();

        var mockScopeFactory = new Moq.Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
        var mockScope = new Moq.Mock<Microsoft.Extensions.DependencyInjection.IServiceScope>();
        var mockProvider = new Moq.Mock<IServiceProvider>();
        mockProvider.Setup(x => x.GetService(typeof(INotificationService))).Returns(notificationService);
        mockProvider.Setup(x => x.GetService(typeof(IForumRepository))).Returns(repository);
        mockScope.Setup(x => x.ServiceProvider).Returns(mockProvider.Object);
        mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);

        var logger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<ForumService>>().Object;

        _service = new ForumService(repository, mockScopeFactory.Object, logger, _currentUserService);
    }

    public async Task InitializeAsync()
    {
        await using var context = _factory.CreateDbContext();
        // Guarantees a 100% clean, empty slate BEFORE each test runs!
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"Posts\", \"Threads\", \"Users\" RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private async Task<User> EnsureUserExistsAsync(string username)
    {
        await using var context = _factory.CreateDbContext();
        var existing = await context.Users.FirstOrDefaultAsync(u => u.UserName == username);
        if (existing != null)
        {
            return existing;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            Email = $"{username.ToLower()}@test.com",
            NormalizedEmail = $"{username.ToLower()}@test.com".ToUpperInvariant(),
            EmailConfirmed = true,
            Role = Roles.Member,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task SetCurrentUserAsync(string username)
    {
        var user = await EnsureUserExistsAsync(username);
        _currentUserService.Username = user.Username;
        _currentUserService.UserId = user.Id;
        _currentUserService.Role = user.Role;
        _currentUserService.IsAuthenticated = true;
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
        // Arrange
        await SetCurrentUserAsync("Developer");

        // Act
        var thread =
            await _service.CreateThreadAsync(1, "Blazor Server TDD", "Unit testing Blazor apps is easy with bUnit.");

        // Assert
        Assert.NotNull(thread);
        Assert.NotEqual(Guid.Empty, thread.Id);
        Assert.Equal("Blazor Server TDD", thread.Title);
        Assert.Equal("Unit testing Blazor apps is easy with bUnit.", thread.Content);
        Assert.Equal("Developer", thread.Author?.Username);
        Assert.Equal(1, thread.CategoryId);
        Assert.Equal(1, thread.Upvotes);
        Assert.Equal(0, thread.Views);
    }

    [Fact]
    public async Task GetThreadsAsync_WithFiltersAndSearchQuery_FiltersAndReturnsMatchingThreads()
    {
        // Create initial threads
        await SetCurrentUserAsync("Alice");
        await _service.CreateThreadAsync(1, "General Chit Chat", "Just checking in.");

        await SetCurrentUserAsync("Bob");
        var t2 = await _service.CreateThreadAsync(2, "C# .NET 10 Web Development", "Discussing the latest features.");

        await SetCurrentUserAsync("Charlie");
        var t3 = await _service.CreateThreadAsync(2, "Vite vs Webpack in ASP.NET", "Frontend comparison.");

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
        await SetCurrentUserAsync("Tester");
        var thread = await _service.CreateThreadAsync(1, "View Count Test", "Does views increment?");

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
        await SetCurrentUserAsync("Voter");
        var thread = await _service.CreateThreadAsync(1, "Upvote Thread Test", "Content");

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
        await SetCurrentUserAsync("Author");
        var thread = await _service.CreateThreadAsync(1, "Quote Test Thread", "Content");

        // Act 1: Create top-level reply post
        await SetCurrentUserAsync("Alice");
        var post1 = await _service.CreatePostAsync(thread.Id, "This is the first post.");
        Assert.NotNull(post1);
        Assert.NotEqual(Guid.Empty, post1.Id);
        Assert.Null(post1.ReplyToPostId);
        Assert.Equal(0, post1.Upvotes);

        // Act 2: Create sub-reply quoting post1
        await SetCurrentUserAsync("Bob");
        var post2 = await _service.CreatePostAsync(thread.Id, "> Alice said: This is the first post.\n\nI agree!",
            replyToPostId: post1.Id);
        Assert.NotNull(post2);
        Assert.Equal(post1.Id, post2.ReplyToPostId);
    }

    [Fact]
    public async Task GetPostsForThreadAsync_WithExistingReplies_ReturnsRepliesInChronologicalOrder()
    {
        await SetCurrentUserAsync("Author");
        var thread = await _service.CreateThreadAsync(1, "Chronology Thread", "Content");

        // Create posts at slightly different times
        await SetCurrentUserAsync("Alice");
        var p1 = await _service.CreatePostAsync(thread.Id, "First reply");

        await SetCurrentUserAsync("Bob");
        var p2 = await _service.CreatePostAsync(thread.Id, "Second reply");

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
        await SetCurrentUserAsync("Author");
        var thread = await _service.CreateThreadAsync(1, "Thread", "Content");

        await SetCurrentUserAsync("Bob");
        var post = await _service.CreatePostAsync(thread.Id, "Reply content");

        // Act
        await _service.UpvotePostAsync(post.Id);

        // Assert
        var posts = await _service.GetPostsForThreadAsync(thread.Id);
        Assert.Single(posts);
        Assert.Equal(1, posts[0].Upvotes);
    }
}
