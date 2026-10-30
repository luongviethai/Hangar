using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Hangar.Application.Builds;
using Hangar.Domain.Entities;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Hangar.Api.Uploads;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;

namespace Hangar.Api.Controllers;

[ApiController]
[Route("api/organizations/{organizationId:int}/apps/{appId:int}/builds")]
[Authorize(Policy = "OrgDeveloper")]
public class BuildsController : ControllerBase
{
    private readonly IBuildService _buildService;
    private readonly UploadSettings _uploadSettings;

    public BuildsController(IBuildService buildService, IOptions<UploadSettings> uploadOptions)
    {
        _buildService = buildService;
        _uploadSettings = uploadOptions.Value;
    }

    [HttpPost]
    // [DisableRequestSizeLimit] tự set IHttpMaxRequestBodySizeFeature.MaxRequestBodySize = null ở giai
    // đoạn Authorization filter (trước action chạy) — không tự set lại trong body action, vì lúc đó
    // Kestrel đã bắt đầu đọc body (do "Expect: 100-continue"), set lại sẽ ném InvalidOperationException.
    [DisableRequestSizeLimit]
    // Không có attribute này, FormValueProviderFactory tự đọc sạch Request.Body trước khi action
    // chạy (xem DisableFormValueModelBindingAttribute) — bắt buộc phải có khi tự đọc multipart thủ công.
    [DisableFormValueModelBinding]
    public async Task<IActionResult> Upload(int appId, CancellationToken ct)
    {
        string tempPath;
        string fileName;
        long sizeBytes;
        try
        {
            (tempPath, fileName, sizeBytes) = await ReadFileSectionToTempAsync(Request, _uploadSettings.MaxSizeBytes, ct);

        }
        catch (FileTooLargeException ex)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, ex.Message);
        }

        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await using var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read);
            var result = await _buildService.UploadAsync(appId, userId, fileName, stream, sizeBytes, "application/octet-stream", ct);
            return result is null ? NotFound() : StatusCode(StatusCodes.Status201Created, result);
        }
        finally
        {
            System.IO.File.Delete(tempPath); // "File" không qua namespace đủ sẽ bị shadow bởi ControllerBase.File(...)
        }
    }

    private static async Task<(string tempFilePath, string fileName, long sizeBytes)> ReadFileSectionToTempAsync(
    HttpRequest request, long maxSizeBytes, CancellationToken ct)
    {
        var boundary = HeaderUtilities.RemoveQuotes(
            MediaTypeHeaderValue.Parse(request.ContentType).Boundary).Value!;
        var reader = new MultipartReader(boundary, request.Body);

        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(ct)) != null)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
                || !disposition.DispositionType.Equals("form-data")
                || string.IsNullOrEmpty(disposition.FileName.Value))
            {
                continue;
            }

            var fileName = disposition.FileName.Value;
            var tempPath = Path.GetTempFileName();
            long total = 0;
            var buffer = new byte[64 * 1024];

            await using var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
            int read;
            while ((read = await section.Body.ReadAsync(buffer, ct)) > 0)
            {
                total += read;
                if (total > maxSizeBytes)
                {
                    await tempStream.DisposeAsync();
                    System.IO.File.Delete(tempPath);
                    throw new FileTooLargeException(maxSizeBytes);
                }
                await tempStream.WriteAsync(buffer.AsMemory(0, read), ct);
            }

            return (tempPath, fileName, total);
        }

        throw new InvalidOperationException("Không tìm thấy file trong multipart body.");
    }

}