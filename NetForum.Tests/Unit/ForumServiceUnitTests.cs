using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NetForum.Data.Repositories;
using NetForum.Services;

namespace NetForum.Tests.Unit;

public partial class ForumServiceUnitTests
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

        mockServiceProvider.Setup(x => x.GetService(typeof(INotificationService)))
            .Returns(_mockNotificationService.Object);
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

    private static bool ContainsBoth(IEnumerable<Guid> list, Guid threadAuthorId, Guid quoteAuthorId)
    {
        var materialized = list.ToList();
        return materialized.Contains(threadAuthorId) && materialized.Contains(quoteAuthorId);
    }
}
