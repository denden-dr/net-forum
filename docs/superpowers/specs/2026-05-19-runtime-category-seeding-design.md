# Design Spec: Runtime Category Seeding in Development Environment

Move the seeding of default categories from EF Core migrations (`OnModelCreating`'s `HasData`) to a runtime seeder that executes when the application runs in a development environment, while keeping integration tests running by also seeding categories programmatically in the test container environment.

## 1. Context & Motivation

Currently, default categories ("General", "Programming", "Q&A / Support", "Announcements") are defined using `modelBuilder.Entity<Category>().HasData(...)` in `AppDbContext.cs`. This generates migrations that hardcode seed data. The user requested that this category seeding should occur inside the development seeder when running in a development environment.

We will move this seeding to the application runtime code and also ensure that integration tests (which run on a clean database container) are programmatically seeded with the same default categories.

## 2. Proposed Changes

### A. Seeding Helper in `DbInitializer.cs`
We will add a new method to check and seed categories:

```csharp
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
```

We will update the entry point to run category seeding before running user seeding:
```csharp
public static async Task SeedDevelopmentDataAsync(this WebApplication app)
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await dbContextFactory.CreateDbContextAsync();
        await SeedCategoriesAsync(context);
    }

    // Existing parallel user seeding tasks:
    await Task.WhenAll(
        EnsureUserSeededInScopeAsync(app, Guid.Parse("00000000-0000-0000-0000-000000000001"), "DevUser", "devuser@netforum.com", "Password123!", Roles.Member),
        EnsureUserSeededInScopeAsync(app, null, "TestMember", "member@netforum.com", "Password123!", Roles.Member),
        EnsureUserSeededInScopeAsync(app, null, "TestModerator", "moderator@netforum.com", "Password123!", Roles.Moderator),
        EnsureUserSeededInScopeAsync(app, null, "TestAdmin", "admin@netforum.com", "Password123!", Roles.Admin)
    );
}
```

### B. Clean Up `AppDbContext.cs`
Remove the category seeding block from `OnModelCreating`.
Generate an EF Core migration `RemoveCategorySeed` to remove it from migrations history.

### C. Update Program.cs
Call `app.SeedDevelopmentDataAsync()` instead of the old `app.SeedDevelopmentUserAsync()`.

### D. Update Test Fixture (`TestDbContextFactory.cs`)
Call `DbInitializer.SeedCategoriesAsync(context)` right after `context.Database.EnsureCreatedAsync()`.

## 3. Testing and Verification Plan

- Run `make build` to check compilation.
- Run `make migration-add name=RemoveCategorySeed` to generate migration.
- Run `make test` to verify all 59 tests compile and pass.
- Run `make run` to ensure local startup works.
