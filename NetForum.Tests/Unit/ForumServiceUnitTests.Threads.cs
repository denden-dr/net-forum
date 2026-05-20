using Moq;
using NetForum.Data;
using NetForum.Data.Entities;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Tests.Unit;

public partial class ForumServiceUnitTests
{
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
}
