using Microsoft.EntityFrameworkCore;
using NetForum.Components;
using NetForum.Data;
using NetForum.Data.Repositories;
using NetForum.Services;
using Microsoft.AspNetCore.Identity;
using NetForum.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Register DB Context Factory using PostgreSQL connection settings
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IForumRepository, ForumRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IForumService, ForumService>();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<ICurrentUserService, DevCurrentUserService>();
}
else
{
    builder.Services.AddScoped<ICurrentUserService, ClaimsCurrentUserService>();
}
builder.Services.AddHttpContextAccessor();

builder.Services.AddIdentity<User, IdentityRole<Guid>>(options => {
    options.SignIn.RequireConfirmedEmail = true;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options => {
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.LoginPath = "/login";
    options.SlidingExpiration = true;
});

builder.Services.AddAuthentication()
    .AddGoogle(options => {
        if (!builder.Environment.IsDevelopment())
        {
            var clientId = builder.Configuration["Authentication:Google:ClientId"];
            var clientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new InvalidOperationException("Google OAuth ClientId and ClientSecret are required in non-development environments.");
            }
        }
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "dummy-client-id";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "dummy-client-secret";
    });

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapPost("/api/auth/login", async (
    [FromForm] string username, 
    [FromForm] string password, 
    SignInManager<User> signInManager,
    UserManager<User> userManager) => 
{
    var user = await userManager.FindByNameAsync(username) ?? await userManager.FindByEmailAsync(username);
    if (user == null) return Results.Redirect("/login?error=InvalidCredentials");

    // Prevent password login for passwordless Google OAuth accounts
    if (!await userManager.HasPasswordAsync(user))
    {
        var logins = await userManager.GetLoginsAsync(user);
        if (logins.Any(l => l.LoginProvider == "Google"))
        {
            return Results.Redirect("/login?error=GoogleAccountRegistered");
        }
    }

    var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);
    if (result.Succeeded) return Results.Redirect("/");
    if (result.IsNotAllowed) return Results.Redirect("/login?error=EmailNotVerified");

    return Results.Redirect("/login?error=InvalidCredentials");
});

app.MapGet("/api/auth/logout", async (SignInManager<User> signInManager) => 
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
});

app.MapGet("/api/auth/google-login", (SignInManager<User> signInManager) => 
{
    var redirectUrl = "/api/auth/google-callback";
    var properties = signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
    return Results.Challenge(properties, ["Google"]);
});

app.MapGet("/api/auth/google-callback", async (
    SignInManager<User> signInManager, 
    UserManager<User> userManager) => 
{
    var info = await signInManager.GetExternalLoginInfoAsync();
    if (info == null) return Results.Redirect("/login?error=GoogleAuthFailed");

    var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true);
    if (result.Succeeded) return Results.Redirect("/");

    // If user doesn't exist, register them
    var email = info.Principal.FindFirstValue(ClaimTypes.Email);
    var name = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email?.Split('@')[0];

    if (email == null) return Results.Redirect("/login?error=GoogleEmailRequired");

    var user = new User { UserName = name, Email = email, EmailConfirmed = true }; // Social logins automatically verified
    var createResult = await userManager.CreateAsync(user);
    if (createResult.Succeeded)
    {
        await userManager.AddLoginAsync(user, info);
        await signInManager.SignInAsync(user, isPersistent: true);
        return Results.Redirect("/");
    }

    return Results.Redirect("/login?error=RegistrationFailed");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Seed developer user and default categories at runtime in Development environment only
if (app.Environment.IsDevelopment())
{
    await app.SeedDevelopmentDataAsync();
}

app.Run();
