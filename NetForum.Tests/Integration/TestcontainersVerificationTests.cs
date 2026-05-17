using Microsoft.EntityFrameworkCore;

namespace NetForum.Tests.Integration;

[Collection("PostgreSqlCollection")]
public class TestcontainersVerificationTests(PostgreSqlTestFixture fixture)
{
    [Fact]
    public async Task AppDbContext_WhenConnectedToPostgresTestcontainer_HasSeedCategoriesOrderedByDisplayOrder()
    {
        // Arrange
        var factory = new TestDbContextFactory(fixture.ConnectionString);

        // Act
        await using var context = factory.CreateDbContext();
        var categories = await context.Categories.OrderBy(c => c.DisplayOrder).ToListAsync();

        // Assert
        Assert.NotNull(categories);
        Assert.Equal(4, categories.Count);
        Assert.Equal("General", categories[0].Name);
        Assert.Equal("Programming", categories[1].Name);
    }
}
