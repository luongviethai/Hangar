namespace Hangar.Domain.Entities;

public class OrganizationMember
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    // Trỏ tới ApplicationUser (Infrastructure/Identity) — chỉ scalar FK, không có navigation
    // property, vì Domain không được phụ thuộc Infrastructure (giống Order.CreatedBy ở CRM).
    // Quan hệ FK thật được cấu hình ở OrganizationMemberConfiguration.
    public int UserId { get; set; }

    public OrganizationRole Role { get; set; }

    public DateTime JoinedAtUtc { get; set; }
}
