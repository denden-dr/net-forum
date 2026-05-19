using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NetForum.Data.Entities;
using Thread = NetForum.Data.Entities.Thread;

namespace NetForum.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) 
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Thread> Threads => Set<Thread>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Notification> Notifications => Set<Notification>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().ToTable("Users");

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

        modelBuilder.Entity<Thread>()
            .HasOne(t => t.Author)
            .WithMany()
            .HasForeignKey(t => t.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

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

        modelBuilder.Entity<Post>()
            .HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Notification
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Recipient)
            .WithMany()
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Sender)
            .WithMany()
            .HasForeignKey(n => n.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Thread)
            .WithMany()
            .HasForeignKey(n => n.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Post)
            .WithMany()
            .HasForeignKey(n => n.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.RecipientId, n.CreatedAt, n.IsRead })
            .IsDescending(false, true, false);

    }
}
