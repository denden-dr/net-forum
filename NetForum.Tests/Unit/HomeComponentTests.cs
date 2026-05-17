using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NetForum.Components.Pages;
using NetForum.Data.Entities;
using NetForum.Services;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Tests.Unit;

public class HomeComponentTests : BunitContext
{
    [Fact]
    public void HomePage_RendersDiscussionHeader()
    {
        // Arrange
        var mockService = new Mock<IForumService>();
        mockService.Setup(s => s.GetCategoriesAsync()).ReturnsAsync([]);
        mockService.Setup(s => s.GetThreadsAsync(null, null)).ReturnsAsync([]);
        Services.AddSingleton(mockService.Object);

        // Act
        var cut = Render<Home>();

        // Assert
        var header = cut.Find("h2");
        Assert.Equal("All Discussions", header.TextContent);
    }

    [Fact]
    public void HomePage_RendersThreadsCorrectly()
    {
        // Arrange
        var mockService = new Mock<IForumService>();
        mockService.Setup(s => s.GetCategoriesAsync()).ReturnsAsync([]);
        
        var category = new Category { Id = 1, Name = "General", Slug = "general" };
        var thread = new Thread
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Category = category,
            Title = "Testing Home Page Rendering",
            Content = "Beautiful Blazor Server design",
            AuthorName = "Tester",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            Upvotes = 3,
            Posts = []
        };
        
        mockService.Setup(s => s.GetThreadsAsync(null, null)).ReturnsAsync([thread]);
        Services.AddSingleton(mockService.Object);

        // Act
        var cut = Render<Home>();

        // Assert
        var threadTitleLink = cut.Find("h4.h5 a");
        Assert.Equal("Testing Home Page Rendering", threadTitleLink.TextContent);
        
        var threadAuthor = cut.Find(".text-muted.small");
        Assert.Contains("Posted by Tester", threadAuthor.TextContent);
    }
}
