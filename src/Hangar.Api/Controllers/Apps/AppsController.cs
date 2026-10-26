using FluentValidation;
using Hangar.Application.Apps;
using Hangar.Application.Apps.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hangar.Api.Controllers;

// {organizationId} trong route là bắt buộc — OrganizationMemberAuthorizationHandler đọc đúng
// giá trị này để check quyền + set ICurrentOrganizationContext cho global query filter.
[ApiController]
[Route("api/organizations/{organizationId:int}/apps")]
public class AppsController : ControllerBase
{
    private readonly IAppService _appService;
    private readonly IValidator<CreateAppRequest> _createValidator;
    private readonly IValidator<UpdateAppRequest> _updateValidator;

    public AppsController(
        IAppService appService,
        IValidator<CreateAppRequest> createValidator,
        IValidator<UpdateAppRequest> updateValidator)
    {
        _appService = appService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    // "OrgDeveloper" = Role tối thiểu Developer — Owner cũng thỏa (xem OrganizationMemberAuthorizationHandler.Satisfies).
    // Theo ma trận quyền: tạo/sửa/xóa App đều Owner+Developer được, không riêng Owner.
    [Authorize(Policy = "OrgDeveloper")]
    [HttpGet]
    public async Task<IActionResult> GetAll(int organizationId)
    {
        var apps = await _appService.GetAllAsync();
        return Ok(apps);
    }

    // Khác các action khác trong controller này — dùng "AppAccess", KHÔNG dùng "OrgDeveloper",
    // vì Tester (không phải OrganizationMember) cũng cần xem được đúng App mình được mời. Chỉ
    // an toàn áp dụng ở action đọc 1 App cụ thể như thế này, không áp dụng ở GetAll (list).
    [Authorize(Policy = "AppAccess")]
    [HttpGet("{appId:int}")]
    public async Task<IActionResult> GetById(int organizationId, int appId)
    {
        var app = await _appService.GetByIdAsync(appId);
        if (app is null)
        {
            return NotFound();
        }

        return Ok(app);
    }

    [Authorize(Policy = "OrgDeveloper")]
    [HttpPost]
    public async Task<IActionResult> Create(int organizationId, [FromBody] CreateAppRequest request)
    {
        var validationResult = await _createValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var result = await _appService.CreateAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.App);
    }

    [Authorize(Policy = "OrgDeveloper")]
    [HttpPut("{appId:int}")]
    public async Task<IActionResult> Update(int organizationId, int appId, [FromBody] UpdateAppRequest request)
    {
        var validationResult = await _updateValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var app = await _appService.UpdateAsync(appId, request);
        if (app is null)
        {
            return NotFound();
        }

        return Ok(app);
    }

    [Authorize(Policy = "OrgDeveloper")]
    [HttpDelete("{appId:int}")]
    public async Task<IActionResult> Delete(int organizationId, int appId)
    {
        var deleted = await _appService.DeleteAsync(appId);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
