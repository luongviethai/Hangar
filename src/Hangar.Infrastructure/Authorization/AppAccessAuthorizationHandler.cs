using System.Security.Claims;
using Hangar.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Hangar.Infrastructure.Authorization;

// CHỈ dùng cho action đọc đúng 1 App cụ thể (route có cả {organizationId} và {appId}) — KHÔNG
// dùng cho action list nhiều App (GetAll), vì Tester chỉ được xem đúng 1 App được mời, không
// phải duyệt toàn bộ Organization.
public class AppAccessAuthorizationHandler : AuthorizationHandler<AppAccessRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentOrganizationContext _currentOrganizationContext;

    public AppAccessAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        IApplicationDbContext context,
        ICurrentOrganizationContext currentOrganizationContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
        _currentOrganizationContext = currentOrganizationContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AppAccessRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        if (!TryGetRouteInt(httpContext, "organizationId", out var organizationId) ||
            !TryGetRouteInt(httpContext, "appId", out var appId))
        {
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
        {
            return;
        }

        var isOrgMember = await _context.OrganizationMembers
            .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId);

        var isAppTester = !isOrgMember && await _context.AppTesters
            .AnyAsync(t => t.AppId == appId && t.UserId == userId);

        if (!isOrgMember && !isAppTester)
        {
            return; // không phải thành viên Organization, cũng không được mời làm tester App này
        }

        // Set context cho global query filter cả 2 trường hợp — an toàn vì requirement này chỉ
        // gắn vào action đọc ĐÚNG 1 App theo appId cụ thể (route), không gắn vào action list —
        // Tester vẫn chỉ nhìn thấy đúng appId đó, không "mở khóa" cả Organization.
        _currentOrganizationContext.OrganizationId = organizationId;
        context.Succeed(requirement);
    }

    private static bool TryGetRouteInt(HttpContext httpContext, string key, out int value)
    {
        value = 0;
        return httpContext.Request.RouteValues.TryGetValue(key, out var raw) &&
               int.TryParse(raw?.ToString(), out value);
    }
}
