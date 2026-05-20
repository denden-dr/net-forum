namespace NetForum.Services;

/// <summary>
/// Abstraction for file storage operations (avatars, attachments).
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads an avatar image and returns the public URL of the stored file.
    /// </summary>
    Task<string> UploadAvatarAsync(Stream fileStream, string fileName, string contentType);

    /// <summary>
    /// Deletes a stored object by its full public URL. No-op if the URL is null/empty or the object does not exist.
    /// </summary>
    Task DeleteObjectByUrlAsync(string objectUrl);
}
