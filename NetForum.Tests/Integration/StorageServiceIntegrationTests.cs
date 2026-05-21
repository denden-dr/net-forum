using Minio;
using Testcontainers.Minio;
using NetForum.Services;

namespace NetForum.Tests.Integration;

public class StorageServiceIntegrationTests : IAsyncLifetime
{
    private MinioContainer _minioContainer = null!;
    private IStorageService _storageService = null!;

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

        _minioContainer = new MinioBuilder("minio/minio:RELEASE.2024-11-07T00-52-20Z")
            .WithUsername("minioadmin")
            .WithPassword("minioadmin")
            .Build();

        await _minioContainer.StartAsync();

        var hostPort = $"{_minioContainer.Hostname}:{_minioContainer.GetMappedPublicPort(9000)}";

        var client = new MinioClient()
            .WithEndpoint(hostPort)
            .WithCredentials("minioadmin", "minioadmin")
            .Build();

        _storageService = new S3StorageService(client, "netforum", $"http://{hostPort}");
    }

    public async Task DisposeAsync()
    {
        if (_minioContainer != null)
        {
            await _minioContainer.StopAsync();
            await _minioContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task UploadAvatarAsync_UploadsSuccessfullyAndReturnsPublicUrl()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        // Act
        var url = await _storageService.UploadAvatarAsync(stream, "test.png", "image/png");

        // Assert
        Assert.NotNull(url);
        Assert.Contains("/netforum/avatars/", url);
        Assert.Contains(".png", url);
    }
}
