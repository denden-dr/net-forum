using Moq;
using NetForum.Data.Entities;
using NetForum.Data.Repositories;
using NetForum.Services;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Tests.Unit;

public class ForumServiceUnitTests
{
    private readonly Mock<IForumRepository> _mockRepository;
    private readonly ForumService _service;

    public ForumServiceUnitTests()
    {
        _mockRepository = new Mock<IForumRepository>();
        _service = new ForumService(_mockRepository.Object);
    }

    [Fact]
    public async Task GetCategoriesAsync_WhenCalled_ReturnsRepositoryCategories()
    {
        // Arrange
        var expectedCategories = new List<Category>
        {
            new Category { Id = 1, Name = "General", Slug = "general", DisplayOrder = 1 },
            new Category { Id = 2, Name = "Programming", Slug = "programming", DisplayOrder = 2 }
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
    public async Task CreateThreadAsync_WithUntrimmedInputsAndEmptyAuthor_TrimsInputsAndDefaultsAuthorToAnonymous()
    {
        // Arrange
        var categoryId = 1;
        var title = "  Untrimmed Title  ";
        var content = "  Untrimmed Content  ";
        var authorName = "   "; // Whitespace should fall back to Anonymous

        Thread? capturedThread = null;
        _mockRepository.Setup(r => r.CreateThreadAsync(It.IsAny<Thread>()))
            .Callback<Thread>(t => capturedThread = t)
            .ReturnsAsync((Thread t) => t);

        // Act
        var thread = await _service.CreateThreadAsync(categoryId, title, content, authorName);

        // Assert
        Assert.NotNull(thread);
        Assert.NotNull(capturedThread);
        Assert.Equal("Untrimmed Title", capturedThread.Title);
        Assert.Equal("Untrimmed Content", capturedThread.Content);
        Assert.Equal("Anonymous", capturedThread.AuthorName);
        Assert.Equal(1, capturedThread.Upvotes);
        _mockRepository.Verify(r => r.CreateThreadAsync(It.IsAny<Thread>()), Times.Once);
    }

    [Fact]
    public async Task GetThreadsAsync_WithCategoryIdAndSearchQuery_DelegatesToRepositoryWithFilters()
    {
        // Arrange
        var categoryId = 2;
        var query = "Vite";
        var expectedThreads = new List<Thread>
        {
            new Thread { Id = Guid.NewGuid(), Title = "Vite vs Webpack" }
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
    public async Task UpvoteThreadAsync_WithValidThreadId_IncrementsUpvotesAndPersists()
    {
        // Arrange
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
    public async Task CreatePostAsync_WithUntrimmedInputsAndEmptyAuthor_TrimsInputsAndDefaultsAuthorToAnonymous()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var content = "   My response text   ";
        var authorName = "";
        var replyToId = Guid.NewGuid();

        Post? capturedPost = null;
        _mockRepository.Setup(r => r.CreatePostAsync(It.IsAny<Post>()))
            .Callback<Post>(p => capturedPost = p)
            .ReturnsAsync((Post p) => p);

        // Act
        var post = await _service.CreatePostAsync(threadId, content, authorName, replyToId);

        // Assert
        Assert.NotNull(post);
        Assert.NotNull(capturedPost);
        Assert.Equal("My response text", capturedPost.Content);
        Assert.Equal("Anonymous", capturedPost.AuthorName);
        Assert.Equal(replyToId, capturedPost.ReplyToPostId);
        Assert.Equal(0, capturedPost.Upvotes);
        _mockRepository.Verify(r => r.CreatePostAsync(It.IsAny<Post>()), Times.Once);
    }

    [Fact]
    public async Task GetPostsForThreadAsync_WithThreadId_DelegatesToRepository()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var expectedPosts = new List<Post>
        {
            new Post { Id = Guid.NewGuid(), Content = "First" },
            new Post { Id = Guid.NewGuid(), Content = "Second" }
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
    public async Task UpvotePostAsync_WithValidPostId_IncrementsUpvotesAndPersists()
    {
        // Arrange
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
}
