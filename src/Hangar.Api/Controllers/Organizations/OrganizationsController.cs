using System.Security.Claims;
using FluentValidation;
using Hangar.Application.Organizations;
using Hangar.Application.Organizations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hangar.Api.Controllers;

[ApiController]
[Route("api/organizations")]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly IValidator<CreateOrganizationRequest> _createValidator;
    private readonly IValidator<InviteMemberRequest> _inviteValidator;
    private readonly IValidator<AcceptInviteRequest> _acceptValidator;

    public OrganizationsController(
        IOrganizationService organizationService,
        IValidator<CreateOrganizationRequest> createValidator,
        IValidator<InviteMemberRequest> inviteValidator,
        IValidator<AcceptInviteRequest> acceptValidator)
    {
        _organizationService = organizationService;
        _createValidator = createValidator;
        _inviteValidator = inviteValidator;
        _acceptValidator = acceptValidator;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest request)
    {
        var validationResult = await _createValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await _organizationService.CreateAsync(request, userId);
        if (!result.Succeeded)
        {
            return BadRequest(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Organization);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var organizations = await _organizationService.GetMyOrganizationsAsync(userId);
        return Ok(organizations);
    }

    // OrgOwner — chỉ Owner mới mời thành viên mới (ma trận quyền), Developer không được.
    [Authorize(Policy = "OrgOwner")]
    [HttpPost("{organizationId:int}/members/invite")]
    public async Task<IActionResult> InviteMember(int organizationId, [FromBody] InviteMemberRequest request)
    {
        var validationResult = await _inviteValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var result = await _organizationService.InviteMemberAsync(organizationId, request);
        if (result is null)
        {
            return NotFound();
        }

        if (!result.Succeeded)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    // Không có {organizationId} trong route — token tự xác định Organization (xem
    // IOrganizationService.AcceptInviteAsync). Chỉ cần [Authorize] thường, KHÔNG dùng
    // OrgOwner/OrgDeveloper — người accept đang XIN GIA NHẬP, chưa phải thành viên.
    [Authorize]
    [HttpPost("invites/accept")]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteRequest request)
    {
        var validationResult = await _acceptValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userEmail = User.FindFirstValue(ClaimTypes.Email)!;

        var result = await _organizationService.AcceptInviteAsync(request.Token, userId, userEmail);
        if (!result.Succeeded)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Organization);
    }
}
