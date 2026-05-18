using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using NetForum.Data;

namespace NetForum.Services;

public class ClaimsCurrentUserService : ICurrentUserService, IDisposable
{
    private readonly AuthenticationStateProvider? _authStateProvider;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<ClaimsCurrentUserService> _logger;
    private ClaimsPrincipal? _cachedPrincipal;

    public ClaimsCurrentUserService(
        AuthenticationStateProvider authStateProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ClaimsCurrentUserService> logger)
    {
        _authStateProvider = authStateProvider;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;

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
                    _logger.LogError(task.Exception, "Error loading initial authentication state asynchronously.");
                }
                else if (task.IsCompletedSuccessfully)
                {
                    var user = task.Result.User;
                    if (user?.Identity?.IsAuthenticated == true)
                    {
                        _cachedPrincipal = user;
                    }
                }
            }, TaskScheduler.Current);
        }
    }

    public Guid? UserId
    {
        get
        {
            var user = GetPrincipal();
            if (user?.Identity?.IsAuthenticated == true)
            {
                var nameIdentifier = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(nameIdentifier, out var parsed))
                {
                    return parsed;
                }
            }
            return null;
        }
    }

    public string Username
    {
        get
        {
            var user = GetPrincipal();
            if (user?.Identity?.IsAuthenticated == true)
            {
                return user.Identity.Name ?? "Anonymous";
            }
            return "Anonymous";
        }
    }

    public string Role
    {
        get
        {
            var user = GetPrincipal();
            if (user?.Identity?.IsAuthenticated == true)
            {
                return user.FindFirst(ClaimTypes.Role)?.Value ?? Roles.Member;
            }
            return Roles.Member;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            var user = GetPrincipal();
            return user?.Identity?.IsAuthenticated ?? false;
        }
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
                _logger.LogError(t.Exception, "Error updating authentication state after change event.");
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

