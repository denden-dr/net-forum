using System.ComponentModel.DataAnnotations;

namespace NetForum.Data.Entities;

public class Thread
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public int CategoryId { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string AuthorName { get; set; } = "Anonymous";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public int Upvotes { get; set; } = 0;

    public int Views { get; set; } = 0;

    public Category? Category { get; set; }

    public ICollection<Post> Posts { get; set; } = new List<Post>();
}
