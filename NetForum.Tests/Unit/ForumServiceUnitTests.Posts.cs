using Moq;
using NetForum.Data;
using NetForum.Data.Entities;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Tests.Unit;

public partial class ForumServiceUnitTests
{
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
}
