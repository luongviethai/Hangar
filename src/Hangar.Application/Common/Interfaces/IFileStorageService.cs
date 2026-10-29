namespace Hangar.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task UploadAsync(string objectKey, Stream content, long sizeBytes, string contentType, CancellationToken ct = default);
    Task DeleteAsync(string objectKey, CancellationToken ct = default);
}
