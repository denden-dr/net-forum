using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using NetForum.Data;

namespace NetForum.Tests.Integration;

public class PostgreSqlTestFixture : IAsyncLifetime
{
    public PostgreSqlContainer? DbContainer { get; private set; }
    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Automatically configure Podman rootless socket path for Fedora/Linux local space
        var podmanSock = "/run/user/1000/podman/podman.sock";
        if (File.Exists(podmanSock))
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", $"unix://{podmanSock}");
        }

        // ryuk resource reaper container requires privileged security contexts under rootless podman
        Environment.SetEnvironmentVariable("RYUK_CONTAINER_PRIVILEGED", "true");

        var container = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("netforum_test_db")
            .WithUsername("test_user")
            .WithPassword("test_pass")
            .Build();

        DbContainer = container;

        // Boot container once
        await container.StartAsync();
        ConnectionString = container.GetConnectionString();

        // Create the database schema once
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync(); // Creates all tables once
        await DbInitializer.SeedCategoriesAsync(context); // Programmatically seed default categories
    }

    public async Task DisposeAsync()
    {
        if (DbContainer != null)
        {
            // Explicitly shut down container after all tests in the collection finish
            await DbContainer.StopAsync();
            await DbContainer.DisposeAsync();
        }
    }
}

[CollectionDefinition("PostgreSqlCollection")]
public class PostgreSqlCollection : ICollectionFixture<PostgreSqlTestFixture>
{
    // Purpose is to apply [CollectionDefinition] and all ICollectionFixture<> interfaces.
}

public class TestDbContextFactory(string connectionString) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
