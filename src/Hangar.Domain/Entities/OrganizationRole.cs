namespace Hangar.Domain.Entities;

// Owner/Developer chỉ có ý nghĩa TRONG 1 Organization cụ thể (xem OrganizationMember) — không
// phải role toàn cục như ApplicationRole ở CRM. Tester không nằm trong enum này — Tester gắn
// theo từng App cụ thể (xem AppTester), không phải vai trò toàn Organization.
public enum OrganizationRole
{
    Owner,
    Developer
}
