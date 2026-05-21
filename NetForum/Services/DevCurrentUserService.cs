using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using NetForum.Data;

namespace NetForum.Services;

public class DevCurrentUserService : ICurrentUserService, IDisposable
{
    public const string DevFallbackUsername = "DevUser";

    private readonly AuthenticationStateProvider? _authStateProvider;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly NavigationManager? _navigationManager;
    private readonly ILogger<DevCurrentUserService> _logger;
    private ClaimsPrincipal? _cachedPrincipal;

    public DevCurrentUserService()
    {
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DevCurrentUserService>.Instance;
    }

    public DevCurrentUserService(ILogger<DevCurrentUserService> logger)
    {
        _logger = logger;
    }

    public DevCurrentUserService(
        AuthenticationStateProvider authStateProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DevCurrentUserService> logger,
        NavigationManager? navigationManager = null)
    {
        _authStateProvider = authStateProvider;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _navigationManager = navigationManager;

        // 1. Initialize immediately with HTTP context user if available
        var httpUser = _httpContextAccessor?.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated == true)
        {
            _cachedPrincipal = httpUser;
        }

        // 2. Subscribe to AuthenticationStateProvider changes and perform non-blocking initialization
        if (_authStateProvider != null)
        {
            _authStateProvider.AuthenticationStateChanged += OnAuthStateChanged;

            _authStateProvider.GetAuthenticationStateAsync().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    _logger.LogError(task.Exception,
                        "Error loading initial authentication state asynchronously in development.");
                }
                else if (task.IsCompletedSuccessfully)
                {
                    var user = task.Result.User;
                    if (user.Identity?.IsAuthenticated == true)
                    {
                        _cachedPrincipal = user;
                    }
                }
            }, TaskScheduler.Current);
        }
    }

    public Guid? TestUserId { get; set; }
    public string? TestUsername { get; set; }
    public string? TestRole { get; set; }
    public bool? TestIsAuthenticated { get; set; }

    private static readonly Guid DevId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid? UserId
    {
        get
        {
            if (TestUserId.HasValue) return TestUserId;

            var user = GetPrincipal();
            if (user?.Identity?.IsAuthenticated == true)
            {
                var nameIdentifier = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(nameIdentifier, out var parsed))
                {
                    return parsed;
                }
            }

            return DevId;
        }
        set => TestUserId = value;
    }

    public string Username
    {
        get
        {
            if (TestUsername != null) return TestUsername;

            var user = GetPrincipal();
            if (user?.Identity?.IsAuthenticated == true)
            {
                return user.Identity.Name ?? "Anonymous";
            }

            return DevFallbackUsername;
        }
        set => TestUsername = value;
    }

    public string Role
    {
        get
        {
            if (TestRole != null) return TestRole;

            var user = GetPrincipal();
            if (user?.Identity?.IsAuthenticated == true)
            {
                return user.FindFirst(ClaimTypes.Role)?.Value ?? Roles.Member;
            }

            return Roles.Member;
        }
        set => TestRole = value;
    }

    public bool IsAuthenticated
    {
        get
        {
            if (TestIsAuthenticated.HasValue) return TestIsAuthenticated.Value;

            var user = GetPrincipal();

            // Do not apply the default developer authentication fallback on authentication pages/endpoints
            var context = _httpContextAccessor?.HttpContext;
            if (context != null)
            {
                var path = context.Request.Path.Value ?? "";
                if (path.StartsWith("/login", StringComparison.OrdinalIgnoreCase) || 
                    path.StartsWith("/register", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
                {
                    return user?.Identity?.IsAuthenticated ?? false;
                }
            }

            if (_navigationManager != null)
            {
                try
                {
                    var uri = new Uri(_navigationManager.Uri);
                    var path = uri.AbsolutePath;
                    if (path.StartsWith("/login", StringComparison.OrdinalIgnoreCase) || 
                        path.StartsWith("/register", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
                    {
                        return user?.Identity?.IsAuthenticated ?? false;
                    }
                }
                catch
                {
                    // Ignore URI parsing issues in test environments
                }
            }

            return user?.Identity?.IsAuthenticated ?? true;
        }
        set => TestIsAuthenticated = value;
    }

    private ClaimsPrincipal? GetPrincipal()
    {
        var contextUser = _httpContextAccessor?.HttpContext?.User;
        if (contextUser?.Identity?.IsAuthenticated == true)
        {
            return contextUser;
        }

        return _cachedPrincipal;
    }

    private void OnAuthStateChanged(Task<AuthenticationState> task)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogError(t.Exception, "Error updating development authentication state after change event.");
            }
            else if (t.IsCompletedSuccessfully)
            {
                _cachedPrincipal = t.Result.User;
            }
        }, TaskScheduler.Current);
    }

    public void Dispose()
    {
        if (_authStateProvider != null)
        {
            _authStateProvider.AuthenticationStateChanged -= OnAuthStateChanged;
        }
    }
}

