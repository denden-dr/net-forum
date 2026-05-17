# 🔌 NetForum - Service API Reference

All business logic, database queries, and data mutations are housed within the thread-safe **Service Layer**. This document serves as the formal specification for our central service interface: `IForumService`.

---

## 🛠️ The `IForumService` Interface

```csharp
namespace NetForum.Services;

public interface IForumService
{
    // Category Operations
    Task<List<Category>> GetCategoriesAsync();
    Task<Category?> GetCategoryBySlugAsync(string slug);

    // Thread Operations
    Task<List<Thread>> GetThreadsAsync(int? categoryId = null, string? searchQuery = null);
    Task<Thread?> GetThreadByIdAsync(Guid threadId, bool incrementViewCount = false);
    Task<Thread> CreateThreadAsync(int categoryId, string title, string content, string authorName);
    Task UpvoteThreadAsync(Guid threadId);

    // Post / Reply Operations
    Task<List<Post>> GetPostsForThreadAsync(Guid threadId);
    Task<Post> CreatePostAsync(Guid threadId, string content, string authorName, Guid? replyToPostId = null);
    Task UpvotePostAsync(Guid postId);
}
```

---

## 📖 Method Specifications & Behavioral Rules

### 1. Categories

#### `GetCategoriesAsync()`
* **Returns:** A list of all available categories.
* **Ordering:** Strictly ordered in ascending sequence by `DisplayOrder`.
* **Usage:** Sidebar category directory menus.

#### `GetCategoryBySlugAsync(string slug)`
* **Parameters:** `slug` (the lowercase URL slug, e.g. `"programming"`).
* **Behavior:** Performs a trimmed, case-insensitive index check on the unique category slugs.
* **Returns:** The matching `Category` or `null` if not found.

---

### 2. Threads

#### `GetThreadsAsync(int? categoryId = null, string? searchQuery = null)`
* **Parameters:**
  * `categoryId` (Optional): Filters threads to a specific category.
  * `searchQuery` (Optional): Filters threads by text query.
* **Behavior:** Performs case-insensitive matching (`.ToLower().Contains()`) against both thread `Title` and `Content` properties.
* **Ordering:** Sorted by `CreatedAt` in descending order (newest first).

#### `GetThreadByIdAsync(Guid threadId, bool incrementViewCount = false)`
* **Behavior:** Retrieves a thread by its unique GUID. If `incrementViewCount` is `true`, it atomically increments the view count by `1` and persists it.

#### `CreateThreadAsync(int categoryId, string title, string content, string authorName)`
* **Behavior:** Sanitizes inputs, trimming whitespace from `Title` and `Content`. If `authorName` is empty, it defaults to `"Anonymous"`.
* **State Initializers:** Sets `CreatedAt` to UTC, `Views` to `0`, and `Upvotes` to `1` (which acts as the initial creator self-upvote).

#### `UpvoteThreadAsync(Guid threadId)`
* **Behavior:** Atomically increments the target thread's `Upvotes` count by `1`.

---

### 3. Posts & Replies

#### `GetPostsForThreadAsync(Guid threadId)`
* **Behavior:** Returns all replies for a thread, including self-referencing links to parent quote blocks (`Include(p => p.ReplyToPost)`).
* **Ordering:** Strictly sorted in ascending chronological order (`CreatedAt` ascending) to represent a natural discussion timeline.

#### `CreatePostAsync(Guid threadId, string content, string authorName, Guid? replyToPostId = null)`
* **Parameters:**
  * `replyToPostId` (Optional): GUID of the parent post being cited/quoted.
* **Behavior:** Sanitizes and trims input text. If `authorName` is blank, it defaults to `"Anonymous"`.

#### `UpvotePostAsync(Guid postId)`
* **Behavior:** Atomically increments the reply's `Upvotes` count by `1`.

---

## 🗄️ The `IForumRepository` Interface Contract

Our persistence interface `IForumRepository` abstracts all raw database queries and mutations:

```csharp
namespace NetForum.Data.Repositories;

public interface IForumRepository
{
    // Category Operations
    Task<List<Category>> GetCategoriesAsync();
    Task<Category?> GetCategoryBySlugAsync(string slug);

    // Thread Operations
    Task<List<Thread>> GetThreadsAsync(int? categoryId = null, string? searchQuery = null);
    Task<Thread?> GetThreadByIdAsync(Guid threadId);
    Task<Thread> CreateThreadAsync(Thread thread);
    Task UpdateThreadAsync(Thread thread);

    // Post / Reply Operations
    Task<List<Post>> GetPostsForThreadAsync(Guid threadId);
    Task<Post> CreatePostAsync(Post post);
    Task UpdatePostAsync(Post post);
    Task<Post?> GetPostByIdAsync(Guid postId);
}
```

#### Key Implementation Details
* **Thread-Safe Factory Access:** Built utilizing `IDbContextFactory<AppDbContext>` to resolve concurrent websocket circuits dynamically.
* **Async Disposals:** Employs `await using var context = ...` to release database resources asynchronously during context termination.
