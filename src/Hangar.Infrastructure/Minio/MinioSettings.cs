namespace Hangar.Infrastructure.Minio;

public class MinioSettings
{
    public string Endpoint { get; set; } = default!;   // "minio:9000" bên trong network Docker
    public string AccessKey { get; set; } = default!;
    public string SecretKey { get; set; } = default!;
    public string BucketName { get; set; } = "hangar-builds";
}
