using Hangar.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Hangar.Infrastructure.Authorization;

// Khác PermissionRequirement ở CRM (check permission TĨNH theo policy name) — requirement này
// cần biết THÊM resource nào đang bị truy cập ({organizationId} trong route), không chỉ tên
// policy, nên không dùng được named policy tra DB như CRM. Chỉ có 2 giá trị MinimumRole khả dĩ
// (Owner/Developer) nên đăng ký 2 policy tĩnh ở Program.cs là đủ, không cần policy provider
// động như PermissionPolicyProvider của CRM.
public class OrganizationMemberRequirement : IAuthorizationRequirement
{
    public OrganizationRole MinimumRole { get; }

    public OrganizationMemberRequirement(OrganizationRole minimumRole)
    {
        MinimumRole = minimumRole;
    }
}
