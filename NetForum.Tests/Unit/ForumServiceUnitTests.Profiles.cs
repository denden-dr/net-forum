using Moq;
using NetForum.Data.Entities;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Tests.Unit;

public partial class ForumServiceUnitTests
{
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
    public async Task GetRecentThreadsByUserAsync_WithSkipAndCount_DelegatesToRepository()
    {
        var userId = Guid.NewGuid();
        var expectedThreads = new List<Thread>
        {
            new() { Id = Guid.NewGuid(), Title = "Thread 1" },
            new() { Id = Guid.NewGuid(), Title = "Thread 2" }
        };
        _mockRepository.Setup(r => r.GetRecentThreadsByUserAsync(userId, 10, 5)).ReturnsAsync(expectedThreads);

        var result = await _service.GetRecentThreadsByUserAsync(userId, 10, 5);

        Assert.Equal(2, result.Count);
        _mockRepository.Verify(r => r.GetRecentThreadsByUserAsync(userId, 10, 5), Times.Once);
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
    public async Task UpdateUserProfileAsync_WithAvatarStream_DeletesOldAvatarBeforeUploading()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldAvatarUrl = "http://minio:9000/netforum/avatars/old-guid.png";
        var user = new User { Id = userId, Username = "Replacer", EmailConfirmed = true, AvatarUrl = oldAvatarUrl };

        _mockCurrentUserService.Setup(u => u.IsAuthenticated).Returns(true);
        _mockCurrentUserService.Setup(u => u.UserId).Returns(userId);
        _mockRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

        _mockStorageService.Setup(s => s.DeleteObjectByUrlAsync(oldAvatarUrl)).Returns(Task.CompletedTask);
        _mockStorageService.Setup(s => s.UploadAvatarAsync(It.IsAny<Stream>(), "new-avatar.jpg", "image/jpeg"))
            .ReturnsAsync("http://minio:9000/netforum/avatars/new-guid.jpg");
        _mockRepository.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        // Act
        using var stream = new MemoryStream(new byte[1024]);
        await _service.UpdateUserProfileAsync("bio", stream, "new-avatar.jpg", "image/jpeg");

        // Assert
        _mockStorageService.Verify(s => s.DeleteObjectByUrlAsync(oldAvatarUrl), Times.Once);
        _mockStorageService.Verify(s => s.UploadAvatarAsync(It.IsAny<Stream>(), "new-avatar.jpg", "image/jpeg"), Times.Once);
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
}
