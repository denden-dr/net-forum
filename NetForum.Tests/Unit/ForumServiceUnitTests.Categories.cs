using Moq;
using NetForum.Data.Entities;

namespace NetForum.Tests.Unit;

public partial class ForumServiceUnitTests
{
    [Fact]
    public async Task GetCategoriesAsync_WhenCalled_ReturnsRepositoryCategories()
    {
        // Arrange
        var expectedCategories = new List<Category>
        {
            new() { Id = 1, Name = "General", Slug = "general", DisplayOrder = 1 },
            new() { Id = 2, Name = "Programming", Slug = "programming", DisplayOrder = 2 }
        };
        _mockRepository.Setup(r => r.GetCategoriesAsync()).ReturnsAsync(expectedCategories);

        // Act
        var categories = await _service.GetCategoriesAsync();

        // Assert
        Assert.NotNull(categories);
        Assert.Equal(expectedCategories.Count, categories.Count);
        Assert.Equal(expectedCategories[0].Name, categories[0].Name);
        _mockRepository.Verify(r => r.GetCategoriesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetCategoryBySlugAsync_WithExistingSlug_ReturnsMatchingCategory()
    {
        // Arrange
        var slug = "programming";
        var expectedCategory = new Category { Id = 2, Name = "Programming", Slug = slug };
        _mockRepository.Setup(r => r.GetCategoryBySlugAsync(slug)).ReturnsAsync(expectedCategory);

        // Act
        var category = await _service.GetCategoryBySlugAsync(slug);

        // Assert
        Assert.NotNull(category);
        Assert.Equal(expectedCategory.Name, category.Name);
        _mockRepository.Verify(r => r.GetCategoryBySlugAsync(slug), Times.Once);
    }
}
