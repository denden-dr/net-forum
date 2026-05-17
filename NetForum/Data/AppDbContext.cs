using Microsoft.EntityFrameworkCore;
using NetForum.Data.Entities;
using Thread = NetForum.Data.Entities.Thread;


namespace NetForum.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Thread> Threads => Set<Thread>();
    public DbSet<Post> Posts => Set<Post>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Category
        modelBuilder.Entity<Category>()
            .HasIndex(c => c.Slug)
            .IsUnique();

        // Configure Thread
        modelBuilder.Entity<Thread>()
            .HasOne(t => t.Category)
            .WithMany(c => c.Threads)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Post
        modelBuilder.Entity<Post>()
            .HasOne(p => p.Thread)
            .WithMany(t => t.Posts)
            .HasForeignKey(p => p.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Post>()
            .HasOne(p => p.ReplyToPost)
            .WithMany()
            .HasForeignKey(p => p.ReplyToPostId)
            .OnDelete(DeleteBehavior.SetNull);

        // Seed Core Categories
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "General", Description = "General chatter, discussions, and off-topic things.", Slug = "general", Icon = "bi-chat-left-dots", DisplayOrder = 1 },
            new Category { Id = 2, Name = "Programming", Description = "Discuss code, web development, algorithms, and tech stacks.", Slug = "programming", Icon = "bi-code-slash", DisplayOrder = 2 },
            new Category { Id = 3, Name = "Q&A / Support", Description = "Got a technical question? Ask the community for help.", Slug = "qa", Icon = "bi-question-circle", DisplayOrder = 3 },
            new Category { Id = 4, Name = "Announcements", Description = "Official updates, guidelines, and site news.", Slug = "announcements", Icon = "bi-megaphone", DisplayOrder = 4 }
        );
    }
}
