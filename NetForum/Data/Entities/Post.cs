using System.ComponentModel.DataAnnotations;

namespace NetForum.Data.Entities;

public class Post
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ThreadId { get; set; }

    public Guid? ReplyToPostId { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required]
    public Guid AuthorId { get; set; }

    public User? Author { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public int Upvotes { get; set; } = 0;

    public Thread? Thread { get; set; }

    public Post? ReplyToPost { get; set; }
}
