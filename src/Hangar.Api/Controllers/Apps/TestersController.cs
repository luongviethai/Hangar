using FluentValidation;
using Hangar.Application.AppTesters;
using Hangar.Application.AppTesters.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hangar.Api.Controllers;

[ApiController]
[Route("api/organizations/{organizationId:int}/apps/{appId:int}/testers")]
public class TestersController : ControllerBase
{
    private readonly IAppTesterService _appTesterService;
    private readonly IValidator<InviteTesterRequest> _inviteValidator;

    public TestersController(IAppTesterService appTesterService, IValidator<InviteTesterRequest> inviteValidator)
    {
        _appTesterService = appTesterService;
        _inviteValidator = inviteValidator;
    }

    // Quản lý danh sách tester là hành động của Owner/Developer (ma trận quyền) — Tester
    // không tự mời/xóa tester khác.
    [Authorize(Policy = "OrgDeveloper")]
    [HttpGet]
    public async Task<IActionResult> GetAll(int organizationId, int appId)
    {
        var testers = await _appTesterService.GetTestersAsync(appId);
        if (testers is null)
        {
            return NotFound();
        }

        return Ok(testers);
    }

    [Authorize(Policy = "OrgDeveloper")]
    [HttpPost]
    public async Task<IActionResult> Invite(int organizationId, int appId, [FromBody] InviteTesterRequest request)
    {
        var validationResult = await _inviteValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var result = await _appTesterService.InviteAsync(appId, request);
        if (result is null)
        {
            return NotFound();
        }

        if (!result.Succeeded)
        {
            return BadRequest(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Tester);
    }

    [Authorize(Policy = "OrgDeveloper")]
    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Remove(int organizationId, int appId, int userId)
    {
        var removed = await _appTesterService.RemoveAsync(appId, userId);
        if (removed is null || removed is false)
        {
            return NotFound();
        }

        return NoContent();
    }
}
