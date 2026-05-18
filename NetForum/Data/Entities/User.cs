using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace NetForum.Data.Entities;

public class User : IdentityUser<Guid>
{
    [NotMapped]
    public string Username
    {
        get => UserName ?? string.Empty;
        set => UserName = value;
    }

    [Required, MaxLength(50)]
    public string Role { get; set; } = Roles.Member;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public int EmailConfirmationRequestsCount { get; set; } = 0;
    public DateTimeOffset? LastEmailConfirmationRequestAt { get; set; }

    // EF Core Navigation Properties - Mapped dynamically by Entity Framework Core to define relationships.
    // Flagged as "unused" in static code analysis because we query authored threads/posts directly from their respective database context tables.
    // ReSharper disable UnusedAutoPropertyAccessor.Global
    // ReSharper disable CollectionNeverUpdated.Global
    public ICollection<Thread> Threads { get; set; } = new List<Thread>();
    public ICollection<Post> Posts { get; set; } = new List<Post>();
}
