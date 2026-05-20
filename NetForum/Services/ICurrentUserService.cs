namespace NetForum.Services;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string Username { get; }
    // ReSharper disable once UnusedMemberInSuper.Global
    string Role { get; }
    bool IsAuthenticated { get; }
}
