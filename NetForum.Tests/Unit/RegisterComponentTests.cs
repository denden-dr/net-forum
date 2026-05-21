using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NetForum.Components.Pages;
using NetForum.Data.Entities;
using NetForum.Services;

namespace NetForum.Tests.Unit;

public class RegisterComponentTests : BunitContext
{
    private readonly Mock<IUserStore<User>> _mockUserStore;
    private readonly Mock<UserManager<User>> _mockUserManager;
    private readonly Mock<ILogger<Register>> _mockLogger;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;

    public RegisterComponentTests()
    {
        _mockUserStore = new Mock<IUserStore<User>>();
        _mockUserManager = new Mock<UserManager<User>>(
            _mockUserStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _mockLogger = new Mock<ILogger<Register>>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();

        Services.AddSingleton(_mockUserManager.Object);
        Services.AddSingleton(_mockLogger.Object);
        Services.AddSingleton(_mockCurrentUserService.Object);
    }

    [Fact]
    public void RegisterPage_WhenUserIsAuthenticated_RedirectsToRoot()
    {
        // Arrange
        _mockCurrentUserService.Setup(s => s.IsAuthenticated).Returns(true);
        var navMan = Services.GetRequiredService<NavigationManager>();

        // Act
        var cut = Render<Register>();

        // Assert
        Assert.Equal("http://localhost/", navMan.Uri);
    }

    [Fact]
    public void RegisterPage_WhenUserIsUnauthenticated_RendersForm()
    {
        // Arrange
        _mockCurrentUserService.Setup(s => s.IsAuthenticated).Returns(false);

        // Act
        var cut = Render<Register>();

        // Assert
        var header = cut.Find("h3");
        Assert.Equal("Create an Account", header.TextContent);
        
        // Assert the form fields exist
        var inputs = cut.FindAll("input");
        Assert.Equal(3, inputs.Count); // Username, Email, Password
    }
}
