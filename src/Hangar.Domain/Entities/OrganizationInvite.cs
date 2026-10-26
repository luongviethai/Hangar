namespace Hangar.Domain.Entities;

// Lời mời có thể gửi tới 1 email CHƯA có tài khoản Hangar — không gắn UserId lúc tạo, chỉ gắn
// Email. Người được mời đăng ký (nếu chưa có) rồi đăng nhập, gọi accept với Token — lúc đó hệ
// thống mới biết UserId thật và tạo OrganizationMember.
public class OrganizationInvite
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public required string Email { get; set; }
    public OrganizationRole Role { get; set; }

    // Hash (SHA-256) của token gửi qua email — không lưu plain text, giống RefreshToken.
    public required string TokenHash { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    // null = chưa accept.
    public DateTime? AcceptedAtUtc { get; set; }
}
