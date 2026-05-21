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

    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await dbContextFactory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
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
