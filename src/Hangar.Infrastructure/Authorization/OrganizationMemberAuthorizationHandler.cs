using System.Security.Claims;
using Hangar.Application.Common.Interfaces;
using Hangar.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Hangar.Infrastructure.Authorization;

public class OrganizationMemberAuthorizationHandler : AuthorizationHandler<OrganizationMemberRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentOrganizationContext _currentOrganizationContext;

    public OrganizationMemberAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        IApplicationDbContext context,
        ICurrentOrganizationContext currentOrganizationContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
        _currentOrganizationContext = currentOrganizationContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganizationMemberRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return; // không có HttpContext (hiếm, ví dụ chạy trong test) -> không thỏa, Fail
        }

        // Lấy {organizationId} từ route — KHÔNG tin OrganizationId nếu client gửi trong body,
        // vì route là nơi duy nhất xác định rõ "đang thao tác resource nào".
        if (!httpContext.Request.RouteValues.TryGetValue("organizationId", out var raw) ||
            !int.TryParse(raw?.ToString(), out var organizationId))
        {
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
        {
            return;
        }

        var membership = await _context.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId);

        if (membership is null)
        {
            return; // không phải thành viên Organization này -> Fail -> controller trả 403
        }

        if (!Satisfies(membership.Role, requirement.MinimumRole))
        {
            return; // là thành viên, nhưng vai trò không đủ (ví dụ Developer gọi action chỉ Owner mới được)
        }

        // Hợp lệ — set context cho global query filter dùng (AppDbContext), RỒI mới Succeed.
        // Thứ tự quan trọng: set TRƯỚC Succeed, không set SAU, để mọi query chạy từ lúc này
        // trong request đã có đúng OrganizationId lọc.
        _currentOrganizationContext.OrganizationId = organizationId;
        context.Succeed(requirement);
    }

    // Owner có mọi quyền Developer có; không dựa vào thứ tự số của enum (dễ gây lỗi âm thầm
    // nếu sau này ai đó đổi thứ tự khai báo enum) — so sánh rõ ràng bằng ý nghĩa thật.
    private static bool Satisfies(OrganizationRole actual, OrganizationRole minimum)
    {
        if (actual == minimum)
        {
            return true;
        }

        return actual == OrganizationRole.Owner;
    }
}
