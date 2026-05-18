using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using NetForum.Data;

namespace NetForum.Services;

public class ClaimsCurrentUserService : ICurrentUserService
{
    private readonly AuthenticationStateProvider? _authStateProvider;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public ClaimsCurrentUserService(
        AuthenticationStateProvider authStateProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _authStateProvider = authStateProvider;
        _httpContextAccessor = httpContextAccessor;
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

        if (_authStateProvider != null)
        {
            try
            {
                var state = _authStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult();
                if (state.User.Identity?.IsAuthenticated == true)
                {
                    return state.User;
                }
            }
            catch (Exception)
            {
                // Suppress exception
            }
        }

        return null;
    }
}
