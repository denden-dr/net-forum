using Moq;
using NetForum.Data.Entities;
using NetForum.Data.Repositories;
using NetForum.Services;

namespace NetForum.Tests.Unit;

public class NotificationServiceUnitTests
{
    private readonly Mock<INotificationRepository> _mockRepository;
    private readonly NotificationService _service;

    public NotificationServiceUnitTests()
    {
        _mockRepository = new Mock<INotificationRepository>();
        _service = new NotificationService(_mockRepository.Object);
    }

    [Fact]
    public async Task ParseAndCreateMentionsAsync_WithValidMentions_CreatesNotificationForMentionedUsers()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        
        var sender = new User { Id = senderId, Username = "SenderUser", EmailConfirmed = true };
        var recipient = new User { Id = recipientId, Username = "RecipientUser", EmailConfirmed = true, NormalizedUserName = "RECIPIENTUSER" };
        var threadId = Guid.NewGuid();
        
        _mockRepository.Setup(r => r.GetUsersByUsernamesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([recipient]);
        
        Notification? capturedNotification = null;
        _mockRepository.Setup(r => r.CreateNotificationAsync(It.IsAny<Notification>()))
            .Callback<Notification>(n => capturedNotification = n)
            .ReturnsAsync((Notification n) => n);

        // Act
        await _service.ParseAndCreateMentionsAsync("Hey @RecipientUser and @SenderUser, please look!", threadId, null, sender);

        // Assert
        Assert.NotNull(capturedNotification);
        Assert.Equal(recipientId, capturedNotification.RecipientId);
        Assert.Equal(senderId, capturedNotification.SenderId);
        Assert.Equal(threadId, capturedNotification.ThreadId);
        Assert.Contains("mentioned you", capturedNotification.ContentPreview);
    }

    [Fact]
    public async Task ParseAndCreateMentionsAsync_WithSelfMention_DoesNotCreateNotificationForSender()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var sender = new User { Id = senderId, Username = "SenderUser", EmailConfirmed = true };
        var threadId = Guid.NewGuid();

        // Act
        await _service.ParseAndCreateMentionsAsync("Hey @SenderUser!", threadId, null, sender);

        // Assert
        _mockRepository.Verify(r => r.CreateNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task MarkNotificationAsReadAsync_TriggersRepositoryUpdate()
    {
        // Arrange
        var notifId = Guid.NewGuid();
        _mockRepository.Setup(r => r.MarkNotificationAsReadAsync(notifId)).Returns(Task.CompletedTask);

        // Act
        await _service.MarkNotificationAsReadAsync(notifId);

        // Assert
        _mockRepository.Verify(r => r.MarkNotificationAsReadAsync(notifId), Times.Once);
    }

    [Fact]
    public async Task MarkAllNotificationsAsReadForUserAsync_TriggersRepositoryUpdate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockRepository.Setup(r => r.MarkAllNotificationsAsReadForUserAsync(userId)).Returns(Task.CompletedTask);

        // Act
        await _service.MarkAllNotificationsAsReadForUserAsync(userId);

        // Assert
        _mockRepository.Verify(r => r.MarkAllNotificationsAsReadForUserAsync(userId), Times.Once);
    }

    [Fact]
    public async Task ParseAndCreateMentionsAsync_Refactored_UsesBatchQueryInsteadOfLoopQueries()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var recipient1Id = Guid.NewGuid();
        var recipient2Id = Guid.NewGuid();
        
        var sender = new User { Id = senderId, Username = "SenderUser" };
        var recipient1 = new User { Id = recipient1Id, Username = "Recipient1" };
        var recipient2 = new User { Id = recipient2Id, Username = "Recipient2" };
        var threadId = Guid.NewGuid();
        
        _mockRepository.Setup(r => r.GetUsersByUsernamesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([recipient1, recipient2]);
            
        _mockRepository.Setup(r => r.CreateNotificationAsync(It.IsAny<Notification>()))
            .ReturnsAsync((Notification n) => n);

        // Act
        await _service.ParseAndCreateMentionsAsync("Hey @Recipient1 and @Recipient2 check this!", threadId, null, sender);

        // Assert
        _mockRepository.Verify(r => r.GetUsersByUsernamesAsync(It.Is<IEnumerable<string>>(list => 
            CheckUsernames(list))), Times.Once);
            
        _mockRepository.Verify(r => r.GetUserByUsernameAsync(It.IsAny<string>()), Times.Never);
        _mockRepository.Verify(r => r.CreateNotificationAsync(It.IsAny<Notification>()), Times.Exactly(2));
    }

    private static bool CheckUsernames(IEnumerable<string> list)
    {
        var materialized = list.ToList();
        return materialized.Contains("Recipient1") && materialized.Contains("Recipient2");
    }
}
