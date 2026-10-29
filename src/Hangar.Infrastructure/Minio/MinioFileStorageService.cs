using Hangar.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Hangar.Infrastructure.Minio;

public class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _minio;
    private readonly string _bucket;

    public MinioFileStorageService(IMinioClient minio, IOptions<MinioSettings> options)
    {
        _minio = minio;
        _bucket = options.Value.BucketName;
    }

    public Task UploadAsync(string objectKey, Stream content, long sizeBytes, string contentType, CancellationToken ct = default)
    {
        var args = new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(objectKey)
            .WithStreamData(content)
            .WithObjectSize(sizeBytes)
            .WithContentType(contentType);
        return _minio.PutObjectAsync(args, ct);
    }

    public Task DeleteAsync(string objectKey, CancellationToken ct = default)
    {
        var args = new RemoveObjectArgs().WithBucket(_bucket).WithObject(objectKey);
        return _minio.RemoveObjectAsync(args, ct);
    }
}
