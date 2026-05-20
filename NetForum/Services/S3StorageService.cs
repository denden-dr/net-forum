using Minio;
using Minio.DataModel.Args;

namespace NetForum.Services;

public class S3StorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;
    private readonly string _publicBaseUrl;

    public S3StorageService(IMinioClient minioClient, string bucketName, string publicBaseUrl)
    {
        _minioClient = minioClient;
        _bucketName = bucketName;
        _publicBaseUrl = publicBaseUrl.TrimEnd('/');
    }

    public async Task<string> UploadAvatarAsync(Stream fileStream, string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName);
        var objectName = $"avatars/{Guid.NewGuid()}{ext}";

        var bucketExists = await _minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_bucketName));
        if (!bucketExists)
        {
            await _minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_bucketName));

            var policyJson = $"{{\"Version\":\"2012-10-17\",\"Statement\":[{{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":[\"s3:GetObject\"],\"Resource\":[\"arn:aws:s3:::{_bucketName}/avatars/*\"]}}]}}";
            await _minioClient.SetPolicyAsync(
                new SetPolicyArgs().WithBucket(_bucketName).WithPolicy(policyJson));
        }

        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(fileStream)
            .WithObjectSize(fileStream.Length)
            .WithContentType(contentType));

        return $"{_publicBaseUrl}/{_bucketName}/{objectName}";
    }
}
