# Runtime Category Seeding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move category seeding from EF Core migration seeding (`HasData` in `OnModelCreating`) to runtime programmatic seeding during development and integration tests.

**Architecture:** We will implement a shared static seeding method `DbInitializer.SeedCategoriesAsync` inside the `NetForum` application. This method will be invoked during startup in development mode, and also in the integration test database fixture after running `EnsureCreatedAsync()`.

**Tech Stack:** .NET 10, EF Core, PostgreSQL

---

### Task 1: Add Category Seeding Helper to DbInitializer

**Files:**
- Modify: `NetForum/Data/DbInitializer.cs`

- [ ] **Step 1: Implement SeedCategoriesAsync and update SeedDevelopmentUserAsync**
  Update `DbInitializer.cs` to add the category seeding logic, and rename `SeedDevelopmentUserAsync` to `SeedDevelopmentDataAsync` to seed both categories and users. Show the complete file.

  ```csharp
  using Microsoft.AspNetCore.Identity;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using NetForum.Data.Entities;

  namespace NetForum.Data;

  public static class DbInitializer
  {
      public static async Task SeedCategoriesAsync(AppDbContext context)
      {
          if (!await context.Categories.AnyAsync())
          {
              context.Categories.AddRange(
                  new Category { Id = 1, Name = "General", Description = "General chatter, discussions, and off-topic things.", Slug = "general", Icon = "bi-chat-left-dots", DisplayOrder = 1 },
                  new Category { Id = 2, Name = "Programming", Description = "Discuss code, web development, algorithms, and tech stacks.", Slug = "programming", Icon = "bi-code-slash", DisplayOrder = 2 },
                  new Category { Id = 3, Name = "Q&A / Support", Description = "Got a technical question? Ask the community for help.", Slug = "qa", Icon = "bi-question-circle", DisplayOrder = 3 },
                  new Category { Id = 4, Name = "Announcements", Description = "Official updates, guidelines, and site news.", Slug = "announcements", Icon = "bi-megaphone", DisplayOrder = 4 }
              );
              await context.SaveChangesAsync();
          }
      }

      public static async Task SeedDevelopmentDataAsync(this WebApplication app)
      {
          using (var scope = app.Services.CreateScope())
          {
              var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
              await using var context = await dbContextFactory.CreateDbContextAsync();
              await SeedCategoriesAsync(context);
          }

          // Execute all user seeding tasks in parallel, each in its own isolated thread-safe service scope
          await Task.WhenAll(
              EnsureUserSeededInScopeAsync(app, Guid.Parse("00000000-0000-0000-0000-000000000001"), "DevUser", "devuser@netforum.com", "Password123!", Roles.Member),
              EnsureUserSeededInScopeAsync(app, null, "TestMember", "member@netforum.com", "Password123!", Roles.Member),
              EnsureUserSeededInScopeAsync(app, null, "TestModerator", "moderator@netforum.com", "Password123!", Roles.Moderator),
              EnsureUserSeededInScopeAsync(app, null, "TestAdmin", "admin@netforum.com", "Password123!", Roles.Admin)
          );
      }

      private static async Task EnsureUserSeededInScopeAsync(WebApplication app, Guid? id, string username, string email, string password, string role)
      {
          using var scope = app.Services.CreateScope();
          var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
          
          var user = id.HasValue 
              ? await userManager.FindByIdAsync(id.Value.ToString()) 
              : await userManager.FindByNameAsync(username);

          if (user == null)
          {
              user = new User
              {
                  Id = id ?? Guid.NewGuid(),
                  UserName = username,
                  NormalizedUserName = username.ToUpperInvariant(),
                  Email = email,
                  NormalizedEmail = email.ToUpperInvariant(),
                  EmailConfirmed = true,
                  SecurityStamp = Guid.NewGuid().ToString(),
                  ConcurrencyStamp = Guid.NewGuid().ToString(),
                  Role = role,
                  CreatedAt = DateTimeOffset.UtcNow
              };
              await userManager.CreateAsync(user, password);
          }
      }
  }
  ```

- [ ] **Step 2: Verify compilation**
  Run: `make build`
  Expected: Successful compilation without errors.

- [ ] **Step 3: Commit**
  ```bash
  git add NetForum/Data/DbInitializer.cs
  git commit -m "feat: add programmatic category seeding helper to DbInitializer"
  ```

---

### Task 2: Update Application Startup

**Files:**
- Modify: `NetForum/Program.cs`

- [ ] **Step 1: Update startup seeding call**
  Change the call from `SeedDevelopmentUserAsync` to `SeedDevelopmentDataAsync` in `Program.cs` (around lines 156-159):

  ```csharp
  // Seed developer user and default categories at runtime in Development environment only
  if (app.Environment.IsDevelopment())
  {
      await app.SeedDevelopmentDataAsync();
  }
  ```

- [ ] **Step 2: Verify compilation**
  Run: `make build`
  Expected: Successful compilation.

- [ ] **Step 3: Commit**
  ```bash
  git add NetForum/Program.cs
  git commit -m "refactor: update startup to call SeedDevelopmentDataAsync"
  ```

---

### Task 3: Make it RED (TDD Verification)

**Files:**
- Modify: `NetForum/Data/AppDbContext.cs`

- [ ] **Step 1: Remove Category migration seeding block**
  Remove the `modelBuilder.Entity<Category>().HasData(...)` seeding from the end of `OnModelCreating` in `AppDbContext.cs` (lines 90-96):

  ```csharp
          // Seed Core Categories
          modelBuilder.Entity<Category>().HasData(
              new Category { Id = 1, Name = "General", Description = "General chatter, discussions, and off-topic things.", Slug = "general", Icon = "bi-chat-left-dots", DisplayOrder = 1 },
              new Category { Id = 2, Name = "Programming", Description = "Discuss code, web development, algorithms, and tech stacks.", Slug = "programming", Icon = "bi-code-slash", DisplayOrder = 2 },
              new Category { Id = 3, Name = "Q&A / Support", Description = "Got a technical question? Ask the community for help.", Slug = "qa", Icon = "bi-question-circle", DisplayOrder = 3 },
              new Category { Id = 4, Name = "Announcements", Description = "Official updates, guidelines, and site news.", Slug = "announcements", Icon = "bi-megaphone", DisplayOrder = 4 }
          );
  ```

- [ ] **Step 2: Run tests to verify test failure (RED)**
  Since the migration seeding is removed, database schema creation during tests will no longer seed default categories, causing integration tests to fail.
  Run: `make test`
  Expected: Failure in integration tests (e.g., `AppDbContext_WhenConnectedToPostgresTestcontainer_HasSeedCategoriesOrderedByDisplayOrder` fails asserting `categories.Count` is 4).

- [ ] **Step 3: Commit**
  ```bash
  git add NetForum/Data/AppDbContext.cs
  git commit -m "test: remove migration data seeding block to trigger RED test state"
  ```

---

### Task 4: Make it GREEN & Generate Migration

**Files:**
- Modify: `NetForum.Tests/Integration/TestDbContextFactory.cs`

- [ ] **Step 1: Call DbInitializer.SeedCategoriesAsync in test fixture**
  Call the shared seeding method in `PostgreSqlTestFixture.InitializeAsync()` (around line 43):

  ```csharp
          await using var context = new AppDbContext(options);
          await context.Database.EnsureDeletedAsync();
          await context.Database.EnsureCreatedAsync(); // Creates all tables once
          await DbInitializer.SeedCategoriesAsync(context); // Programmatically seed categories for integration tests
  ```

- [ ] **Step 2: Run tests to verify they pass (GREEN)**
  Run: `make test`
  Expected: All 59 tests pass successfully.

- [ ] **Step 3: Generate the EF Core migration to remove seed data from migrations**
  Run: `make migration-add name=RemoveCategorySeed`
  Expected: Successful generation of a new migration that removes the seeded categories from previous migrations.

- [ ] **Step 4: Apply migrations to the local development database**
  Run: `make migration-update`
  Expected: Successful execution.

- [ ] **Step 5: Run the application locally**
  Run: `make run`
  Expected: Server starts up successfully, categories are seeded at runtime, and pages load properly.

- [ ] **Step 6: Final Commit**
  ```bash
  git add NetForum.Tests/Integration/TestDbContextFactory.cs NetForum/Data/Migrations/
  git commit -m "feat: enable programmatic seeding in integration tests and generate EF Core migration"
  ```
