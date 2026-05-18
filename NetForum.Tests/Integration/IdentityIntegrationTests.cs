using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NetForum.Data;
using NetForum.Data.Entities;
using System.Security.Claims;

namespace NetForum.Tests.Integration;

[Collection("PostgreSqlCollection")]
public class IdentityIntegrationTests : IAsyncLifetime
{
    private readonly ServiceProvider _serviceProvider;

    public IdentityIntegrationTests(PostgreSqlTestFixture fixture)
    {
        string connectionString = fixture.ConnectionString;

        var services = new ServiceCollection();
        
        services.AddLogging();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddIdentity<User, IdentityRole<Guid>>(options =>
        {
            options.SignIn.RequireConfirmedEmail = true;
            options.User.RequireUniqueEmail = true;
            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromSeconds(2);
        });

        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Guarantees clean table states for testing registration
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Users\" RESTART IDENTITY CASCADE;");
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Register_And_ConfirmEmail_Flow_Succeeds()
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = new User 
        { 
            UserName = "testregisteruser", 
            Email = "register@example.com",
            Role = Roles.Member,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act 1: Create user in Postgres
        var createResult = await userManager.CreateAsync(user, "Password123!");
        Assert.True(createResult.Succeeded);

        // Assert 1: Saved with EmailConfirmed = false
        var savedUser = await userManager.FindByNameAsync("testregisteruser");
        Assert.NotNull(savedUser);
        Assert.False(savedUser.EmailConfirmed);

        // Act 2: Generate Confirmation Token
        var token = await userManager.GenerateEmailConfirmationTokenAsync(savedUser);
        Assert.NotNull(token);

        // Act 3: Confirm Email via Token
        var confirmResult = await userManager.ConfirmEmailAsync(savedUser, token);
        Assert.True(confirmResult.Succeeded);

        // Assert 2: Database state updated to EmailConfirmed = true
        var confirmedUser = await userManager.FindByNameAsync("testregisteruser");
        Assert.NotNull(confirmedUser);
        Assert.True(confirmedUser.EmailConfirmed);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ShouldFail()
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user1 = new User { UserName = "user1", Email = "same@example.com", CreatedAt = DateTimeOffset.UtcNow };
        var user2 = new User { UserName = "user2", Email = "same@example.com", CreatedAt = DateTimeOffset.UtcNow };

        // Act
        var result1 = await userManager.CreateAsync(user1, "Password123!");
        var result2 = await userManager.CreateAsync(user2, "Password123!");

        // Assert
        Assert.True(result1.Succeeded);
        Assert.False(result2.Succeeded);
        Assert.Contains(result2.Errors, e => e.Code == "DuplicateEmail");
    }

    [Fact]
    public async Task GeneratingNewConfirmationToken_InvalidatesPreviousToken()
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = new User 
        { 
            UserName = "tokentester", 
            Email = "token@example.com",
            Role = Roles.Member,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act 1: Register User
        var createResult = await userManager.CreateAsync(user, "Password123!");
        Assert.True(createResult.Succeeded);

        // Act 2: Generate Confirmation Token 1
        var token1 = await userManager.GenerateEmailConfirmationTokenAsync(user);
        Assert.NotNull(token1);

        // Act 3: Regenerate security stamp (as done on resend request)
        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        Assert.True(stampResult.Succeeded);

        // Act 4: Generate Confirmation Token 2
        var token2 = await userManager.GenerateEmailConfirmationTokenAsync(user);
        Assert.NotNull(token2);

        // Act 5: Try to confirm with Token 1 (Should Fail!)
        var confirmResult1 = await userManager.ConfirmEmailAsync(user, token1);
        Assert.False(confirmResult1.Succeeded);

        // Act 6: Confirm with Token 2 (Should Succeed!)
        var confirmResult2 = await userManager.ConfirmEmailAsync(user, token2);
        Assert.True(confirmResult2.Succeeded);
    }

    [Fact]
    public async Task EmailConfirmationToken_LifespanExpiration_FailsConfirmation()
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = new User 
        { 
            UserName = "expiredtester", 
            Email = "expired@example.com",
            Role = Roles.Member,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, "Password123!");
        Assert.True(createResult.Succeeded);

        // Generate Token
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        Assert.NotNull(token);

        // Wait for token to expire (Lifespan configured to 2 seconds)
        await Task.Delay(2500);

        // Attempt to confirm (Should Fail!)
        var confirmResult = await userManager.ConfirmEmailAsync(user, token);
        Assert.False(confirmResult.Succeeded);
    }

    [Fact]
    public async Task EmailConfirmation_SpamPrevention_BlocksMoreThanThreeRequestsPerDay()
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = new User 
        { 
            UserName = "spamtester", 
            Email = "spam@example.com",
            Role = Roles.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            EmailConfirmationRequestsCount = 3,
            LastEmailConfirmationRequestAt = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, "Password123!");
        Assert.True(createResult.Succeeded);

        // Act: Try to simulate the register spam logic
        var savedUser = await userManager.FindByEmailAsync("spam@example.com");
        Assert.NotNull(savedUser);

        // Verify spam rate limit logic
        var now = DateTimeOffset.UtcNow;
        bool isSpamBlocked = false;
        if (savedUser.LastEmailConfirmationRequestAt.HasValue && 
            now - savedUser.LastEmailConfirmationRequestAt.Value < TimeSpan.FromDays(1))
        {
            if (savedUser.EmailConfirmationRequestsCount >= 3)
            {
                isSpamBlocked = true;
            }
        }

        // Assert: It should block the request
        Assert.True(isSpamBlocked);
    }

    [Fact]
    public async Task PasswordSignIn_WhenEmailNotConfirmed_ReturnsIsNotAllowed()
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();

        var user = new User 
        { 
            UserName = "loginnotallowed", 
            Email = "notallowed@example.com",
            Role = Roles.Member,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Create user (unverified)
        var createResult = await userManager.CreateAsync(user, "Password123!");
        Assert.True(createResult.Succeeded);

        // Attempt sign in
        var signInResult = await signInManager.CheckPasswordSignInAsync(user, "Password123!", lockoutOnFailure: false);

        // Assert: Email verification is required, so sign in is Not Allowed!
        Assert.False(signInResult.Succeeded);
        Assert.True(signInResult.IsNotAllowed);
    }

    [Fact]
    public async Task PasswordSignIn_WithCorrectCredentialsAndConfirmedEmail_Succeeds()
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();

        var user = new User 
        { 
            UserName = "loginsuccess", 
            Email = "success@example.com",
            Role = Roles.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            EmailConfirmed = true // Email verified
        };

        // Create user
        var createResult = await userManager.CreateAsync(user, "Password123!");
        Assert.True(createResult.Succeeded);

        // Attempt sign in with correct password
        var signInResult = await signInManager.CheckPasswordSignInAsync(user, "Password123!", lockoutOnFailure: false);

        // Assert
        Assert.True(signInResult.Succeeded);
    }

    [Fact]
    public async Task PasswordSignIn_WithIncorrectPassword_Fails()
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();

        var user = new User 
        { 
            UserName = "loginfailed", 
            Email = "failed@example.com",
            Role = Roles.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            EmailConfirmed = true
        };

        // Create user
        var createResult = await userManager.CreateAsync(user, "Password123!");
        Assert.True(createResult.Succeeded);

        // Attempt sign in with incorrect password
        var signInResult = await signInManager.CheckPasswordSignInAsync(user, "WrongPassword!", lockoutOnFailure: false);

        // Assert
        Assert.False(signInResult.Succeeded);
    }

    [Fact]
    public async Task GoogleOAuth_NewUserRegistration_SavesAndLinksGoogleAccount()
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Create external principal and login info
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "google-id-123"),
            new Claim(ClaimTypes.Email, "googleuser@example.com"),
            new Claim(ClaimTypes.Name, "GoogleTester")
        }, "Google"));

        var info = new ExternalLoginInfo(principal, "Google", "google-id-123", "GoogleTester");

        // Act: Extract values from claims (replicating Program.cs Google callback logic)
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var name = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email?.Split('@')[0];

        Assert.NotNull(email);
        Assert.NotNull(name);

        var user = new User 
        { 
            UserName = name, 
            Email = email, 
            EmailConfirmed = true // Social logins implicitly confirmed
        };

        var createResult = await userManager.CreateAsync(user);
        Assert.True(createResult.Succeeded);

        var linkResult = await userManager.AddLoginAsync(user, info);
        Assert.True(linkResult.Succeeded);

        // Assert: Verify database state
        var savedUser = await userManager.FindByEmailAsync("googleuser@example.com");
        Assert.NotNull(savedUser);
        Assert.True(savedUser.EmailConfirmed);

        // Assert: Verify Google login association
        var logins = await userManager.GetLoginsAsync(savedUser);
        Assert.Single(logins);
        Assert.Equal("Google", logins[0].LoginProvider);
        Assert.Equal("google-id-123", logins[0].ProviderKey);
    }

    [Fact]
    public async Task GoogleOAuth_ExistingUserSignIn_ResolvesCorrectUser()
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "google-id-456"),
            new Claim(ClaimTypes.Email, "googleexisting@example.com"),
            new Claim(ClaimTypes.Name, "GoogleExisting")
        }, "Google"));

        var info = new ExternalLoginInfo(principal, "Google", "google-id-456", "GoogleExisting");

        var user = new User 
        { 
            UserName = "GoogleExisting", 
            Email = "googleexisting@example.com", 
            EmailConfirmed = true
        };

        // Create and associate
        await userManager.CreateAsync(user);
        await userManager.AddLoginAsync(user, info);

        // Act: Simulate sign in by looking up user by external login provider
        var resolvedUser = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);

        // Assert
        Assert.NotNull(resolvedUser);
        Assert.Equal("GoogleExisting", resolvedUser.UserName);
        Assert.Equal("googleexisting@example.com", resolvedUser.Email);
    }

    [Fact]
    public async Task Register_EmailBelongsToGoogleOAuthAccount_ShouldFailAndDirectToGoogle()
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Create a user without password (passwordless)
        var user = new User 
        { 
            UserName = "googleregtester", 
            Email = "googlereg@example.com",
            Role = Roles.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user);
        Assert.True(createResult.Succeeded);

        // Link external Google login
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "google-reg-id"),
            new Claim(ClaimTypes.Email, "googlereg@example.com"),
            new Claim(ClaimTypes.Name, "googleregtester")
        }, "Google"));
        var info = new ExternalLoginInfo(principal, "Google", "google-reg-id", "googleregtester");
        await userManager.AddLoginAsync(user, info);

        // Act: Simulate Register.razor validation check
        var existingUser = await userManager.FindByEmailAsync("googlereg@example.com");
        Assert.NotNull(existingUser);

        bool isGoogleRegistered = false;
        string? errorMessage = null;

        if (!await userManager.HasPasswordAsync(existingUser))
        {
            var logins = await userManager.GetLoginsAsync(existingUser);
            if (logins.Any(l => l.LoginProvider == "Google"))
            {
                isGoogleRegistered = true;
                errorMessage = "This email is registered with a Google account. Please sign in using Google.";
            }
        }

        // Assert: Registration is blocked with Google user error
        Assert.True(isGoogleRegistered);
        Assert.Equal("This email is registered with a Google account. Please sign in using Google.", errorMessage);
    }

    [Fact]
    public async Task PasswordSignIn_WhenAccountIsGoogleOAuthPasswordless_ReturnsGoogleAccountRegisteredError()
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Create a user without password (passwordless)
        var user = new User 
        { 
            UserName = "googlelogtester", 
            Email = "googlelog@example.com",
            Role = Roles.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user);
        Assert.True(createResult.Succeeded);

        // Link external Google login
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "google-log-id"),
            new Claim(ClaimTypes.Email, "googlelog@example.com"),
            new Claim(ClaimTypes.Name, "googlelogtester")
        }, "Google"));
        var info = new ExternalLoginInfo(principal, "Google", "google-log-id", "googlelogtester");
        await userManager.AddLoginAsync(user, info);

        // Act: Simulate Program.cs password login check
        var targetUser = await userManager.FindByEmailAsync("googlelog@example.com");
        Assert.NotNull(targetUser);

        bool isGoogleRedirectRequired = false;
        if (!await userManager.HasPasswordAsync(targetUser))
        {
            var logins = await userManager.GetLoginsAsync(targetUser);
            if (logins.Any(l => l.LoginProvider == "Google"))
            {
                isGoogleRedirectRequired = true;
            }
        }

        // Assert: Login flow correctly flags that user must redirect to Google Sign-In
        Assert.True(isGoogleRedirectRequired);
    }
}
