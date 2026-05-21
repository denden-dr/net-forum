using Microsoft.AspNetCore.Components;
using NetForum.Data;
using NetForum.Data.Entities;
using NetForum.Services;

namespace NetForum.Tests.Unit;

public class IdentityUnitTests
{
    [Fact]
    public void User_Initialization_Defaults_Role_To_Member()
    {
        // Act
        var user = new User
        {
            UserName = "testuser",
            Email = "test@example.com"
        };

        // Assert
        Assert.Equal(Roles.Member, user.Role);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public void DevCurrentUserService_WhenTestContextSet_ReturnsSetValues()
    {
        // Arrange
        var devService = new DevCurrentUserService();
        var testId = Guid.NewGuid();
        var testUsername = "Alice";
        var testRole = Roles.Admin;

        // Act
        devService.TestUserId = testId;
        devService.TestUsername = testUsername;
        devService.TestRole = testRole;
        devService.TestIsAuthenticated = true;

        // Assert
        Assert.Equal(testId, devService.UserId);
        Assert.Equal(testUsername, devService.Username);
        Assert.Equal(testRole, devService.Role);
        Assert.True(devService.IsAuthenticated);
    }

    [Fact]
    public void DevCurrentUserService_WhenNoAuthenticatedSession_ReturnsDefaultDevUser()
    {
        // Arrange
        var devService = new DevCurrentUserService();

        // Assert (when not overridden, falls back to DevUser fallback)
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), devService.UserId);
        Assert.Equal("DevUser", devService.Username);
        Assert.Equal(Roles.Member, devService.Role);
        Assert.True(devService.IsAuthenticated);
    }

    private class TestNavManager : NavigationManager
    {
        public TestNavManager(string baseUri, string uri)
        {
            Initialize(baseUri, uri);
        }
    }

    [Fact]
    public void DevCurrentUserService_WhenPathIsLoginOrRegister_DoesNotApplyDevFallback()
    {
        // Arrange
        var mockHttpContextAccessor = new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Path = "/login";
        mockHttpContextAccessor.Setup(h => h.HttpContext).Returns(context);

        var devService = new DevCurrentUserService(
            null!, // authStateProvider
            mockHttpContextAccessor.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DevCurrentUserService>.Instance,
            null // navigationManager
        );

        // Assert
        Assert.False(devService.IsAuthenticated);
    }

    [Fact]
    public void DevCurrentUserService_WhenPathIsUnrelatedPrefix_AppliesDevFallback()
    {
        // Arrange
        var mockHttpContextAccessor = new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Path = "/login-help";
        mockHttpContextAccessor.Setup(h => h.HttpContext).Returns(context);

        var devService = new DevCurrentUserService(
            null!, // authStateProvider
            mockHttpContextAccessor.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DevCurrentUserService>.Instance,
            null // navigationManager
        );

        // Assert
        Assert.True(devService.IsAuthenticated);
    }

    [Fact]
    public void DevCurrentUserService_WhenNavUriIsLoginOrRegister_DoesNotApplyDevFallback()
    {
        // Arrange
        var navManager = new TestNavManager("http://localhost/", "http://localhost/login");
        var devService = new DevCurrentUserService(
            null!,
            null!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DevCurrentUserService>.Instance,
            navManager
        );

        // Assert
        Assert.False(devService.IsAuthenticated);
    }

    [Fact]
    public void DevCurrentUserService_WhenNavUriIsUnrelatedPrefix_AppliesDevFallback()
    {
        // Arrange
        var navManager = new TestNavManager("http://localhost/", "http://localhost/login-help");
        var devService = new DevCurrentUserService(
            null!,
            null!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DevCurrentUserService>.Instance,
            navManager
        );

        // Assert
        Assert.True(devService.IsAuthenticated);
    }
}

