using Microsoft.AspNetCore.Authorization;

namespace Hangar.Infrastructure.Authorization;

// Khác OrganizationMemberRequirement (chỉ check vai trò trong Organization) — requirement này
// cho phép THÊM 1 đường hợp lệ nữa: Tester không phải thành viên Organization, nhưng được mời
// riêng vào đúng 1 App (AppTester) thì vẫn xem được App đó — không xem được App khác cùng org.
public class AppAccessRequirement : IAuthorizationRequirement
{
}
