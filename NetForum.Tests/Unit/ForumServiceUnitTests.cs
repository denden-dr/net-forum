using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NetForum.Data;
using NetForum.Data.Entities;
using NetForum.Data.Repositories;
using NetForum.Services;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Tests.Unit;

public class ForumServiceUnitTests
{
    private readonly Mock<IForumRepository> _mockRepository;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly ForumService _service;

    public ForumServiceUnitTests()
    {
        _mockRepository = new Mock<IForumRepository>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockStorageService = new Mock<IStorageService>();
        
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockLogger = new Mock<ILogger<ForumService>>();

        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        mockServiceProvider.Setup(x => x.GetService(typeof(INotificationService))).Returns(_mockNotificationService.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(IForumRepository))).Returns(_mockRepository.Object);

        mockScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);

        _service = new ForumService(
            _mockRepository.Object, 
            mockScopeFactory.Object, 
            mockLogger.Object, 
            _mockCurrentUserService.Object,
            _mockStorageService.Object);
    }

    [Fact]
    public async Task GetCategoriesAsync_WhenCalled_ReturnsRepositoryCategories()
    {
        // Arrange
        var expectedCategories = new List<Category>
        {
            new() { Id = 1, Name = "General", Slug = "general", DisplayOrder = 1 },
            new() { Id = 2, Name = "Programming", Slug = "programming", DisplayOrder = 2 }
        };
        _mockRepository.Setup(r => r.GetCategoriesAsync()).ReturnsAsync(expectedCategories);

        // Act
        var categories = await _service.GetCategoriesAsync();

        // Assert
        Assert.NotNull(categories);
        Assert.Equal(expectedCategories.Count, categories.Count);
        Assert.Equal(expectedCategories[0].Name, categories[0].Name);
        _mockRepository.Verify(r => r.GetCategoriesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetCategoryBySlugAsync_WithExistingSlug_ReturnsMatchingCategory()
    {
        // Arrange
        var slug = "programming";
        var expectedCategory = new Category { Id = 2, Name = "Programming", Slug = slug };
        _mockRepository.Setup(r => r.GetCategoryBySlugAsync(slug)).ReturnsAsync(expectedCategory);

        // Act
        var category = await _service.GetCategoryBySlugAsync(slug);

        // Assert
        Assert.NotNull(category);
        Assert.Equal(expectedCategory.Name, category.Name);
        _mockRepository.Verify(r => r.GetCategoryBySlugAsync(slug), Times.Once);
    }

    [Fact]
    public async Task GetThreadsAsync_WithCategoryIdAndSearchQuery_DelegatesToRepositoryWithFilters()
    {
        // Arrange
        var categoryId = 2;
        var query = "Vite";
        var expectedThreads = new List<Thread>
        {
            new() { Id = Guid.NewGuid(), Title = "Vite vs Webpack" }
        };
        _mockRepository.Setup(r => r.GetThreadsAsync(categoryId, query)).ReturnsAsync(expectedThreads);

        // Act
        var result = await _service.GetThreadsAsync(categoryId, query);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(expectedThreads[0].Title, result[0].Title);
        _mockRepository.Verify(r => r.GetThreadsAsync(categoryId, query), Times.Once);
    }

    [Fact]
    public async Task GetThreadByIdAsync_WithIncrementViewsTrue_IncrementsViewsAndPersists()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var thread = new Thread { Id = threadId, Views = 5 };
        _mockRepository.Setup(r => r.GetThreadByIdAsync(threadId)).ReturnsAsync(thread);
        _mockRepository.Setup(r => r.UpdateThreadAsync(thread)).Returns(Task.CompletedTask);

        // Act
        var result = await _service.GetThreadByIdAsync(threadId, incrementViewCount: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(6, result.Views);
        _mockRepository.Verify(r => r.GetThreadByIdAsync(threadId), Times.Once);
        _mockRepository.Verify(r => r.UpdateThreadAsync(thread), Times.Once);
    }

    [Fact]
    public async Task GetPostsForThreadAsync_WithThreadId_DelegatesToRepository()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var expectedPosts = new List<Post>
        {
            new() { Id = Guid.NewGuid(), Content = "First" },
            new() { Id = Guid.NewGuid(), Content = "Second" }
        };
        _mockRepository.Setup(r => r.GetPostsForThreadAsync(threadId)).ReturnsAsync(expectedPosts);

        // Act
        var posts = await _service.GetPostsForThreadAsync(threadId);

        // Assert
        Assert.NotNull(posts);
        Assert.Equal(2, posts.Count);
        _mockRepository.Verify(r => r.GetPostsForThreadAsync(threadId), Times.Once);
    }

    [Fact]
    public async Task CreateThreadAsync_WhenAnonymous_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.CreateThreadAsync(1, "Title", "Content"));
    }

    [Fact]
    public async Task CreateThreadAsync_WhenAuthenticated_CreatesThreadWithAuthor()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);
        _mockCurrentUserService.Setup(u => u.Username).Returns("MemberUser");

        var user = new User { Id = userId, Username = "MemberUser", EmailConfirmed = true };
        _mockRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

        Thread? capturedThread = null;
        _mockRepository.Setup(r => r.CreateThreadAsync(It.IsAny<Thread>()))
            .Callback<Thread>(t => capturedThread = t)
            .ReturnsAsync((Thread t) => t);

        // Act
        var thread = await _service.CreateThreadAsync(1, "  My Title  ", "  My Content  ");

        // Assert
        Assert.NotNull(thread);
        Assert.NotNull(capturedThread);
        Assert.Equal("My Title", capturedThread.Title);
        Assert.Equal("My Content", capturedThread.Content);
        Assert.Equal(userId, capturedThread.AuthorId);
        Assert.Equal(1, capturedThread.Upvotes);
        _mockRepository.Verify(r => r.CreateThreadAsync(It.IsAny<Thread>()), Times.Once);
    }

    [Fact]
    public async Task CreatePostAsync_WhenAnonymous_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.CreatePostAsync(Guid.NewGuid(), "Content"));
    }

    [Fact]
    public async Task CreatePostAsync_WhenAuthenticated_CreatesPostWithAuthor()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var replyToId = Guid.NewGuid();

        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);
        _mockCurrentUserService.Setup(u => u.Username).Returns("MemberUser");

        var user = new User { Id = userId, Username = "MemberUser", EmailConfirmed = true };
        _mockRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

        Post? capturedPost = null;
        _mockRepository.Setup(r => r.CreatePostAsync(It.IsAny<Post>()))
            .Callback<Post>(p => capturedPost = p)
            .ReturnsAsync((Post p) => p);

        // Act
        var post = await _service.CreatePostAsync(threadId, "  Reply content  ", replyToId);

        // Assert
        Assert.NotNull(post);
        Assert.NotNull(capturedPost);
        Assert.Equal("Reply content", capturedPost.Content);
        Assert.Equal(userId, capturedPost.AuthorId);
        Assert.Equal(replyToId, capturedPost.ReplyToPostId);
        Assert.Equal(0, capturedPost.Upvotes);
        _mockRepository.Verify(r => r.CreatePostAsync(It.IsAny<Post>()), Times.Once);
    }

    [Fact]
    public async Task UpvoteThreadAsync_WhenAnonymous_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.UpvoteThreadAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpvoteThreadAsync_WhenAuthenticated_IncrementsUpvotesAndPersists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);
        
        var user = new User { Id = userId, Username = "VoterUser", EmailConfirmed = true };
        _mockRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

        var threadId = Guid.NewGuid();
        var thread = new Thread { Id = threadId, Upvotes = 4 };
        _mockRepository.Setup(r => r.GetThreadByIdAsync(threadId)).ReturnsAsync(thread);
        _mockRepository.Setup(r => r.UpdateThreadAsync(thread)).Returns(Task.CompletedTask);

        // Act
        await _service.UpvoteThreadAsync(threadId);

        // Assert
        Assert.Equal(5, thread.Upvotes);
        _mockRepository.Verify(r => r.GetThreadByIdAsync(threadId), Times.Once);
        _mockRepository.Verify(r => r.UpdateThreadAsync(thread), Times.Once);
    }

    [Fact]
    public async Task UpvotePostAsync_WhenAnonymous_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.UpvotePostAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpvotePostAsync_WhenAuthenticated_IncrementsUpvotesAndPersists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);

        var user = new User { Id = userId, Username = "VoterUser", EmailConfirmed = true };
        _mockRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

        var postId = Guid.NewGuid();
        var post = new Post { Id = postId, Upvotes = 10 };
        _mockRepository.Setup(r => r.GetPostByIdAsync(postId)).ReturnsAsync(post);
        _mockRepository.Setup(r => r.UpdatePostAsync(post)).Returns(Task.CompletedTask);

        // Act
        await _service.UpvotePostAsync(postId);

        // Assert
        Assert.Equal(11, post.Upvotes);
        _mockRepository.Verify(r => r.GetPostByIdAsync(postId), Times.Once);
        _mockRepository.Verify(r => r.UpdatePostAsync(post), Times.Once);
    }

    [Fact]
    public async Task CreateThread_Throws_UnauthorizedAccessException_When_Email_Not_Confirmed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);

        var unconfirmedUser = new User 
        { 
            Id = userId,
            EmailConfirmed = false,
            Role = Roles.Member
        };

        _mockRepository.Setup(r => r.GetUserByIdAsync(unconfirmedUser.Id)).ReturnsAsync(unconfirmedUser);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            _service.CreateThreadAsync(1, "Title", "Content")
        );
    }

    [Fact]
    public async Task CreatePost_Throws_UnauthorizedAccessException_When_Email_Not_Confirmed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);

        var unconfirmedUser = new User 
        { 
            Id = userId,
            EmailConfirmed = false,
            Role = Roles.Member
        };

        _mockRepository.Setup(r => r.GetUserByIdAsync(unconfirmedUser.Id)).ReturnsAsync(unconfirmedUser);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            _service.CreatePostAsync(Guid.NewGuid(), "Content")
        );
    }

    [Fact]
    public async Task UpvoteThread_Throws_UnauthorizedAccessException_When_Email_Not_Confirmed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);

        var unconfirmedUser = new User 
        { 
            Id = userId,
            EmailConfirmed = false,
            Role = Roles.Member
        };

        _mockRepository.Setup(r => r.GetUserByIdAsync(unconfirmedUser.Id)).ReturnsAsync(unconfirmedUser);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            _service.UpvoteThreadAsync(Guid.NewGuid())
        );
    }

    [Fact]
    public async Task UpvotePost_Throws_UnauthorizedAccessException_When_Email_Not_Confirmed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);

        var unconfirmedUser = new User 
        { 
            Id = userId,
            EmailConfirmed = false,
            Role = Roles.Member
        };

        _mockRepository.Setup(r => r.GetUserByIdAsync(unconfirmedUser.Id)).ReturnsAsync(unconfirmedUser);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            _service.UpvotePostAsync(Guid.NewGuid())
        );
    }

    [Fact]
    public async Task IsCurrentEmailConfirmedAsync_WhenAnonymous_ReturnsFalse()
    {
        // Arrange
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(false);

        // Act
        var result = await _service.IsCurrentEmailConfirmedAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsCurrentEmailConfirmedAsync_WhenAuthenticatedAndNotConfirmed_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);

        var unconfirmedUser = new User { Id = userId, EmailConfirmed = false };
        _mockRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(unconfirmedUser);

        // Act
        var result = await _service.IsCurrentEmailConfirmedAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsCurrentEmailConfirmedAsync_WhenAuthenticatedAndConfirmed_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);

        var confirmedUser = new User { Id = userId, EmailConfirmed = true };
        _mockRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(confirmedUser);

        // Act
        var result = await _service.IsCurrentEmailConfirmedAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CreateThreadAsync_TriggersParseAndCreateMentionsAsync()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "ThreadCreator", EmailConfirmed = true };
        var categoryId = 1;
        var title = "Blazor TDD Thread";
        var content = "This is a post mentioning @SomeUser";
        
        var thread = new Thread { Id = Guid.NewGuid(), Title = title, Content = content, AuthorId = userId };

        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);
        _mockRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);
        _mockRepository.Setup(r => r.CreateThreadAsync(It.IsAny<Thread>())).ReturnsAsync(thread);

        _mockNotificationService.Setup(n => n.ParseAndCreateMentionsAsync(content, thread.Id, null, user))
            .Returns(Task.CompletedTask);

        // Act
        var created = await _service.CreateThreadAsync(categoryId, title, content);

        // Wait briefly for fire-and-forget background task
        await Task.Delay(100);

        // Assert
        Assert.NotNull(created);
        _mockNotificationService.Verify(n => n.ParseAndCreateMentionsAsync(content, created.Id, null, user), Times.Once);
    }

    [Fact]
    public async Task CreatePostAsync_WhenReplyingToThread_TriggersThreadAuthorNotificationAndMentions()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var replierId = Guid.NewGuid();
        
        var author = new User { Id = authorId, Username = "ThreadAuthor", EmailConfirmed = true };
        var replier = new User { Id = replierId, Username = "Replier", EmailConfirmed = true };
        
        var threadId = Guid.NewGuid();
        var thread = new Thread { Id = threadId, AuthorId = authorId, Author = author };
        var post = new Post { Id = Guid.NewGuid(), ThreadId = threadId, AuthorId = replierId, Content = "Normal reply text quoting @Someone" };

        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(replierId);
        _mockRepository.Setup(r => r.GetUserByIdAsync(replierId)).ReturnsAsync(replier);
        _mockRepository.Setup(r => r.GetThreadByIdAsync(threadId)).ReturnsAsync(thread);
        _mockRepository.Setup(r => r.CreatePostAsync(It.IsAny<Post>())).ReturnsAsync(post);

        _mockNotificationService.Setup(n => n.CreateNotificationAsync(authorId, replierId, threadId, post.Id, It.IsAny<string>(), NotificationType.ThreadReply))
            .Returns(Task.CompletedTask);
        _mockNotificationService.Setup(n => n.ParseAndCreateMentionsAsync("Normal reply text quoting @Someone", threadId, post.Id, replier, It.IsAny<IEnumerable<Guid>>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.CreatePostAsync(threadId, "Normal reply text quoting @Someone");
        
        // Wait briefly for background fire-and-forget task to complete
        await Task.Delay(100);

        // Assert
        _mockNotificationService.Verify(n => n.CreateNotificationAsync(authorId, replierId, threadId, post.Id, It.IsAny<string>(), NotificationType.ThreadReply), Times.Once);
        _mockNotificationService.Verify(n => n.ParseAndCreateMentionsAsync("Normal reply text quoting @Someone", threadId, post.Id, replier, It.IsAny<IEnumerable<Guid>>()), Times.Once);
    }

    [Fact]
    public async Task CreatePostAsync_TriggersParseAndCreateMentionsAsync_WithThreadAndQuoteAuthorsExcluded()
    {
        // Arrange
        var threadAuthorId = Guid.NewGuid();
        var quoteAuthorId = Guid.NewGuid();
        var replierId = Guid.NewGuid();
        
        var threadAuthor = new User { Id = threadAuthorId, Username = "ThreadAuthor", EmailConfirmed = true };
        var quoteAuthor = new User { Id = quoteAuthorId, Username = "QuoteAuthor", EmailConfirmed = true };
        var replier = new User { Id = replierId, Username = "Replier", EmailConfirmed = true };
        
        var threadId = Guid.NewGuid();
        var thread = new Thread { Id = threadId, AuthorId = threadAuthorId, Author = threadAuthor };
        
        var parentPostId = Guid.NewGuid();
        var parentPost = new Post { Id = parentPostId, ThreadId = threadId, AuthorId = quoteAuthorId, Author = quoteAuthor };
        
        var content = "This is a reply mentioning @ThreadAuthor and @QuoteAuthor and @SomeoneElse";
        var post = new Post { Id = Guid.NewGuid(), ThreadId = threadId, AuthorId = replierId, Content = content, ReplyToPostId = parentPostId };

        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(replierId);
        _mockRepository.Setup(r => r.GetUserByIdAsync(replierId)).ReturnsAsync(replier);
        _mockRepository.Setup(r => r.GetThreadByIdAsync(threadId)).ReturnsAsync(thread);
        _mockRepository.Setup(r => r.GetPostByIdAsync(parentPostId)).ReturnsAsync(parentPost);
        _mockRepository.Setup(r => r.CreatePostAsync(It.IsAny<Post>())).ReturnsAsync(post);

        // Act
        await _service.CreatePostAsync(threadId, content, parentPostId);
        
        // Wait briefly for background fire-and-forget task to complete
        await Task.Delay(100);

        // Assert
        _mockNotificationService.Verify(n => n.ParseAndCreateMentionsAsync(
            content, 
            threadId, 
            post.Id, 
            replier, 
            It.Is<IEnumerable<Guid>>(list => ContainsBoth(list, threadAuthorId, quoteAuthorId))
        ), Times.Once);
    }

    private static bool ContainsBoth(IEnumerable<Guid> list, Guid threadAuthorId, Guid quoteAuthorId)
    {
        var materialized = list.ToList();
        return materialized.Contains(threadAuthorId) && materialized.Contains(quoteAuthorId);
    }

    [Fact]
    public async Task GetUserProfileAsync_WithUsername_DelegatesToRepository()
    {
        var expectedUser = new User { Id = Guid.NewGuid(), Username = "TestUser", Bio = "Hello" };
        _mockRepository.Setup(r => r.GetUserByUsernameAsync("TestUser")).ReturnsAsync(expectedUser);

        var result = await _service.GetUserProfileAsync("TestUser");

        Assert.NotNull(result);
        Assert.Equal("TestUser", result.Username);
        _mockRepository.Verify(r => r.GetUserByUsernameAsync("TestUser"), Times.Once);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_WhenAnonymous_ThrowsUnauthorizedAccessException()
    {
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.UpdateUserProfileAsync("new bio", null, null, null));
    }

    [Fact]
    public async Task UpdateUserProfileAsync_WhenEmailNotConfirmed_ThrowsUnauthorizedAccessException()
    {
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);

        var unconfirmedUser = new User { Id = userId, EmailConfirmed = false };
        _mockRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(unconfirmedUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.UpdateUserProfileAsync("bio", null, null, null));
    }

    [Fact]
    public async Task UpdateUserProfileAsync_WithAvatarStream_UploadsAndUpdatesUser()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "Uploader", EmailConfirmed = true, AvatarUrl = "/old.jpg" };

        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);
        _mockRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

        _mockStorageService.Setup(s => s.UploadAvatarAsync(It.IsAny<Stream>(), "photo.jpg", "image/jpeg"))
            .ReturnsAsync("http://minio:9000/netforum/avatars/new-guid.jpg");

        _mockRepository.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        using var stream = new MemoryStream(new byte[1024]);
        await _service.UpdateUserProfileAsync("updated bio", stream, "photo.jpg", "image/jpeg");

        Assert.Equal("updated bio", user.Bio);
        Assert.Equal("http://minio:9000/netforum/avatars/new-guid.jpg", user.AvatarUrl);
        _mockStorageService.Verify(s => s.UploadAvatarAsync(It.IsAny<Stream>(), "photo.jpg", "image/jpeg"), Times.Once);
        _mockRepository.Verify(r => r.UpdateUserAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_WithoutAvatarStream_PreservesExistingAvatarUrl()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "BioEditor", EmailConfirmed = true, AvatarUrl = "/existing-avatar.jpg" };

        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);
        _mockRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);
        _mockRepository.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        await _service.UpdateUserProfileAsync("new bio only", null, null, null);

        Assert.Equal("new bio only", user.Bio);
        Assert.Equal("/existing-avatar.jpg", user.AvatarUrl);
        _mockStorageService.Verify(s => s.UploadAvatarAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_WithOversizedFile_ThrowsInvalidOperationException()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "BigUploader", EmailConfirmed = true };

        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);
        _mockRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

        using var stream = new MemoryStream(new byte[6 * 1024 * 1024]); // 6 MB
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateUserProfileAsync("bio", stream, "big.jpg", "image/jpeg"));

        Assert.Contains("5 MB", ex.Message);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_WithInvalidContentType_ThrowsInvalidOperationException()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "BadType", EmailConfirmed = true };

        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);
        _mockRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

        using var stream = new MemoryStream(new byte[1024]);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateUserProfileAsync("bio", stream, "virus.exe", "application/octet-stream"));

        Assert.Contains("PNG, JPEG, or WebP", ex.Message);
    }
}
