using System.ComponentModel.DataAnnotations;

namespace NetForum.Data.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid RecipientId { get; set; }
    public User? Recipient { get; set; }

    [Required]
    public Guid SenderId { get; set; }
    public User? Sender { get; set; }

    [Required]
    public Guid ThreadId { get; set; }
    public Thread? Thread { get; set; }

    public Guid? PostId { get; set; }
    public Post? Post { get; set; }

    [Required, MaxLength(255)]
    public string ContentPreview { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
