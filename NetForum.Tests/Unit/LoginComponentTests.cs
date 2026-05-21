using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NetForum.Components.Pages;
using NetForum.Services;
using System.Collections.Generic;

namespace NetForum.Tests.Unit;

public class LoginComponentTests : BunitContext
{
    [Fact]
    public void LoginPage_WhenGoogleClientIdIsConfigured_RendersActiveGoogleLoginLink()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?> {
            {"Authentication:Google:ClientId", "real-client-id"}
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        Services.AddSingleton(configuration);

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        mockCurrentUserService.Setup(s => s.IsAuthenticated).Returns(false);
        Services.AddSingleton(mockCurrentUserService.Object);

        // Act
        var cut = Render<Login>();

        // Assert
        var googleLink = cut.Find("a[href='/api/auth/google-login']");
        Assert.NotNull(googleLink);
        Assert.Contains("Continue with Google", googleLink.TextContent);
        
        // Assert warning is NOT rendered
        var alerts = cut.FindAll(".alert-warning");
        Assert.Empty(alerts);
    }

    [Fact]
    public void LoginPage_WhenGoogleClientIdIsDummy_RendersDisabledGoogleButtonAndWarning()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?> {
            {"Authentication:Google:ClientId", "dummy-client-id"}
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        Services.AddSingleton(configuration);

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        mockCurrentUserService.Setup(s => s.IsAuthenticated).Returns(false);
        Services.AddSingleton(mockCurrentUserService.Object);

        // Act
        var cut = Render<Login>();

        // Assert
        var disabledButton = cut.Find("button[disabled]");
        Assert.NotNull(disabledButton);
        Assert.Contains("Continue with Google", disabledButton.TextContent);
        
        var warningAlert = cut.Find(".alert-warning");
        Assert.Contains("Google OAuth is not configured for local development", warningAlert.TextContent);
        
        // Assert link is NOT rendered
        var googleLinks = cut.FindAll("a[href='/api/auth/google-login']");
        Assert.Empty(googleLinks);
    }

    [Fact]
    public void LoginPage_WhenGoogleClientIdIsEmpty_RendersDisabledGoogleButtonAndWarning()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?> {
            {"Authentication:Google:ClientId", ""}
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        Services.AddSingleton(configuration);

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        mockCurrentUserService.Setup(s => s.IsAuthenticated).Returns(false);
        Services.AddSingleton(mockCurrentUserService.Object);

        // Act
        var cut = Render<Login>();

        // Assert
        var disabledButton = cut.Find("button[disabled]");
        Assert.NotNull(disabledButton);
        
        var warningAlert = cut.Find(".alert-warning");
        Assert.Contains("Google OAuth is not configured for local development", warningAlert.TextContent);
    }

    [Fact]
    public void LoginPage_WhenUserIsAuthenticated_RedirectsToRoot()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        Services.AddSingleton(configuration);

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        mockCurrentUserService.Setup(s => s.IsAuthenticated).Returns(true);
        Services.AddSingleton(mockCurrentUserService.Object);

        var navMan = Services.GetRequiredService<NavigationManager>();

        // Act
        var cut = Render<Login>();

        // Assert
        Assert.Equal("http://localhost/", navMan.Uri);
    }
}
