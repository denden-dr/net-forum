using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetForum.Components.Pages;
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

        // Act
        var cut = Render<Login>();

        // Assert
        var disabledButton = cut.Find("button[disabled]");
        Assert.NotNull(disabledButton);
        
        var warningAlert = cut.Find(".alert-warning");
        Assert.Contains("Google OAuth is not configured for local development", warningAlert.TextContent);
    }
}
