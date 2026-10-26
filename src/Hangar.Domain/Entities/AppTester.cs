namespace Hangar.Domain.Entities;

// Tester KHÔNG phải vai trò toàn Organization (khác Owner/Developer ở OrganizationMember) —
// tester chỉ được cấp quyền xem/tải build của ĐÚNG 1 App cụ thể được mời, giống mô hình
// TestFlight/Firebase App Distribution thật. 1 user có thể là tester của nhiều App khác nhau,
// mỗi App 1 lời mời riêng.
public class AppTester
{
    public int Id { get; set; }

    // Chỉ scalar FK, không navigation property tới App — App có global query filter
    // (multi-tenant), nếu khai báo navigation "required" tới 1 entity có filter, EF Core cảnh
    // báo kết quả có thể sai khi entity bị filter ra ngoài (CoreEventId...QueryFilterInteraction
    // Warning). Không dùng tới navigation này ở đâu trong code, bỏ hẳn cho sạch model.
    public int AppId { get; set; }

    // Trỏ tới ApplicationUser — chỉ scalar FK, không navigation (giống OrganizationMember.UserId).
    public int UserId { get; set; }

    public DateTime InvitedAtUtc { get; set; }
}
