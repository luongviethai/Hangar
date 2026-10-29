namespace Hangar.Application.Builds;

public interface IBuildService
{
    Task<CreateBuildResult?> UploadAsync(int appId, int uploadedByUserId, string fileName, Stream content, long sizeBytes, string contentType, CancellationToken ct = default);
}
