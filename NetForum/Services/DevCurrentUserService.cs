using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using NetForum.Data;

namespace NetForum.Services;

public class DevCurrentUserService : ICurrentUserService
{
    private readonly AuthenticationStateProvider? _authStateProvider;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public DevCurrentUserService()
    {
    }

    public DevCurrentUserService(
        AuthenticationStateProvider authStateProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _authStateProvider = authStateProvider;
        _httpContextAccessor = httpContextAccessor;
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
            return "DevUser";
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
                // Suppress exception during sync retrieval in background threads
            }
        }

        return null;
    }
}
