# 🧪 NetForum - Testing Architecture

This document describes the Test-Driven Development (TDD) strategy, test matrix, and Postgres 18 container orchestration architecture.

---

## 📈 Strict Test-Driven Development (TDD) Workflow

NetForum strictly adheres to the TDD loop:
1. **Red (Failing):** Write an isolated test covering a new validation, query, or UI state before touching production code. Run `make test` to confirm compilation and verify the failure is logical.
2. **Green (Passing):** Implement the minimal necessary logic to make the test pass.
3. **Refactor:** Clean up class dependencies, reduce redundancy, and format code under fully verified passing conditions.

---

## 📂 The Test Separation Matrix

To optimize speed and correctness, NetForum splits tests into two distinct directories and namespaces:

### 1. Unit Tests (`NetForum.Tests/Unit/`)
* **`ForumServiceUnitTests.cs` & `ForumServiceUnitTests.Profiles.cs`**: Focuses on validating functional business logic operations in the service layer (e.g. upvoting boundaries, casing slug formatting, write blocks for unconfirmed email users, active session checking, profile updates, and avatar uploads). Verified with Moq assertions to ensure that old avatar deletion happens *only after* database updates succeed. Includes a specific unit test testing stream validation fallback logic via a custom non-seekable stream throwing `NotSupportedException` on `.Length`.
* **`NotificationServiceUnitTests.cs`**: Validates notification logic, including unread count aggregations, bulk marking as read, and mention parsing behavior using mocked contexts.
* **`IdentityUnitTests.cs`**: Focuses on validating user default property states (e.g. role defaults to `Roles.Member` and `EmailConfirmed = false`) and the thread-safe unauthenticated session fallback mapping rules within `DevCurrentUserService`.
* **`HomeComponentTests.cs`**: Tests UI layout, state transitions, and HTML component hierarchies using **bUnit**. Mocks the `IForumService` interface to avoid executing any database code, yielding sub-millisecond execution times.
* **To run only Unit Tests:** Execute the fast RAM-only target:
* 
  ```bash
  make test-unit
  ```

### 2. Integration Tests (`NetForum.Tests/Integration/`)
* **`ForumServiceIntegrationTests.cs`**: Verifies full database entity interactions, Npgsql driver configurations, cascading database deletes, and transactional unit of work patterns using `ForumRepository` integrated against real PostgreSQL containers.
* **`NotificationServiceIntegrationTests.cs`**: Verifies full integration of the notification system with the persistent PostgreSQL container, including bulk updates and cascades.
* **`IdentityIntegrationTests.cs`**: Integrates the database context with ASP.NET Core Identity's dynamic `UserManager<User>` and cryptographic data protection provider to test user creation, registration flows, unique email constraints, and advanced verification security link lifecycle scenarios (such as token invalidation on regeneration and token lifespan expiration).
* **Authentication Context Simulation:** Utilizes `SetCurrentUserAsync(string username, bool emailConfirmed = true)` inside integration tests to dynamically register and sign in custom integration test users, validating full relational safety.
* **`TestcontainersVerificationTests.cs`**: Confirms that EF Core PostgreSQL migration histories match the target server, checks startup latency, and verifies default seed categories exist.
* **`StorageServiceIntegrationTests.cs`**: Verifies AWS SDK S3 client file upload integration using an isolated containerized MinIO test instance, ensuring files are uploaded successfully and return valid public storage URLs.
* **To run only Integration Tests:** Execute the container-backed target:
  ```bash
  make test-integration
  ```

---

## 🐳 PostgreSQL 18 Testcontainers Lifecycle

We utilize Docker/Podman containerization via `Testcontainers.PostgreSql` to run tests against an exact reproduction of the production database.

### 1. Single Startup Cycle (`PostgreSqlTestFixture`)
To avoid starting a separate container per test (which takes 2-3 seconds each), we implement an xUnit **Collection Fixture** (`ICollectionFixture<PostgreSqlTestFixture>`). 

The container starts **once** before the first test runs, applies EF Core schema migrations, and stops **once** after all 47 tests in the assembly complete.

```csharp
public class PostgreSqlTestFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Auto-configure Podman rootless socket for Linux environments
        var podmanSock = "/run/user/1000/podman/podman.sock";
        if (System.IO.File.Exists(podmanSock))
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", $"unix://{podmanSock}");
        }

        DbContainer = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await DbContainer.StartAsync();
    }
}
```

### 2. Zero-Overhead Automated Truncation (IAsyncLifetime)
To guarantee a completely clean, empty state without repeating manual truncation calls inside each test body, our integration test class implements **`IAsyncLifetime`**. 

This executes a high-performance raw SQL truncation call **automatically before each test runs**:

```csharp
public class ForumServiceIntegrationTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        using var context = _factory.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"Posts\", \"Threads\" RESTART IDENTITY CASCADE;"
        );
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
```

#### Why Truncation is Superior:
* **Speed:** Re-creating a PostgreSQL schema takes roughly **1.5 seconds**. Truncating dynamic tables takes less than **1 millisecond**.
* **Identity Reset:** `RESTART IDENTITY` resets serial and auto-increment keys to their defaults.
* **Preserves Seeds:** Cascading truncation ignores our lookup `Categories` table, keeping seed rows intact for queries.

---

## 🐳 S3-Compatible MinIO Testcontainers Lifecycle

For testing file storage behaviors without polluting development or production buckets, we run a containerized MinIO instance during integration tests.

### 1. Isolated Container Startup (`StorageServiceIntegrationTests`)

In `StorageServiceIntegrationTests`, the MinIO container starts dynamically using the `MinioBuilder` provided by `Testcontainers.Minio`. It generates a temporary bucket and auto-configures the `S3StorageService` instance to point to the host-mapped ports:

```csharp
_minioContainer = new MinioBuilder("minio/minio:latest")
    .WithUsername("minioadmin")
    .WithPassword("minioadmin")
    .Build();

await _minioContainer.StartAsync();

var hostPort = $"{_minioContainer.Hostname}:{_minioContainer.GetMappedPublicPort(9000)}";
_storageService = new S3StorageService(client, "netforum", $"http://{hostPort}");
```

### 2. Automatic Cleanup

Once all tests in `StorageServiceIntegrationTests` finish, `DisposeAsync` stops and destroys the container, ensuring zero lingering side-effects:

```csharp
public async Task DisposeAsync()
{
    if (_minioContainer != null)
    {
        await _minioContainer.StopAsync();
        await _minioContainer.DisposeAsync();
    }
}
```
