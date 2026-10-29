using Hangar.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Hangar.Domain.Entities;
namespace Hangar.Application.Builds;

public class BuildService : IBuildService
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;

    public BuildService(IApplicationDbContext context, IFileStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<CreateBuildResult?> UploadAsync(int appId, int uploadedByUserId, string fileName, Stream content, long sizeBytes, string contentType, CancellationToken ct = default)
    {
        var appExists = await _context.Apps.AnyAsync(a => a.Id == appId, ct);
        if (!appExists) return null; // global query filter đã tự lọc đúng Organization

        var objectKey = $"apps/{appId}/{Guid.NewGuid():N}-{fileName}";
        await _storage.UploadAsync(objectKey, content, sizeBytes, contentType, ct);

        try
        {
            var build = new Build
            {
                AppId = appId,
                FileName = fileName,
                ObjectKey = objectKey,
                SizeBytes = sizeBytes,
                UploadedByUserId = uploadedByUserId,
                CreatedAtUtc = DateTime.UtcNow
            };
            _context.Builds.Add(build);
            await _context.SaveChangesAsync(ct);

            return new CreateBuildResult(build.Id, build.FileName, build.SizeBytes, build.CreatedAtUtc);
        }
        catch
        {
            await _storage.DeleteAsync(objectKey, ct); // compensating action — MinIO không tự rollback được
            throw;
        }
    }
}
