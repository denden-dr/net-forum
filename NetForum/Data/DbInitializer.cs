using Microsoft.AspNetCore.Identity;
using NetForum.Data.Entities;

namespace NetForum.Data;

public static class DbInitializer
{
    public static async Task SeedDevelopmentUserAsync(this WebApplication app)
    {
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
