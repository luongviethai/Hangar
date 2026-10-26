namespace Hangar.Application.Common.Interfaces;

// Scoped (1 instance/request) — null nghĩa là "chưa xác định Organization nào cho request này".
// OrganizationMemberAuthorizationHandler set giá trị này SAU KHI xác nhận user là thành viên
// hợp lệ; AppDbContext dùng giá trị này làm global query filter cho App (xem AppDbContext).
public interface ICurrentOrganizationContext
{
    int? OrganizationId { get; set; }
}
