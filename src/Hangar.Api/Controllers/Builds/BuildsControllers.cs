using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Hangar.Application.Builds;
using Hangar.Domain.Entities;
using System.Security.Claims;


namespace Hangar.Api.Controllers;

[ApiController]
[Route("api/organizations/{organizationId:int}/apps/{appId:int}/builds")]
[Authorize(Policy = "OrgDeveloper")]
public class BuildsController : ControllerBase
{
    private readonly IBuildService _buildService;

    public BuildsController(IBuildService buildService) => _buildService = buildService;

    [HttpPost]
    [RequestSizeLimit(200_000_000)] // tạm 200MB — Ngày 10 sẽ làm đúng bài limit + 413
    public async Task<IActionResult> Upload(int appId, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Thiếu file hoặc file rỗng.");

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        using var stream = file.OpenReadStream();

        var result = await _buildService.UploadAsync(appId, userId, file.FileName, stream, file.Length, file.ContentType, ct);

        return result is null ? NotFound() : CreatedAtAction(nameof(Upload), new { id = result.Id }, result);
    }
}