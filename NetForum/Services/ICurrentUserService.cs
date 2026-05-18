namespace NetForum.Services;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string Username { get; }
    string Role { get; }
    bool IsAuthenticated { get; }
}
