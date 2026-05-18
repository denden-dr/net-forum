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
}
