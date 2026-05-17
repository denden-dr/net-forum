using System.ComponentModel.DataAnnotations;

namespace NetForum.Data.Entities;

public class Category
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Icon { get; set; } = "bi-tag";

    public int DisplayOrder { get; set; } = 0;

    public ICollection<Thread> Threads { get; set; } = new List<Thread>();
}
