namespace Hangar.Infrastructure.Identity;

// Không phải business entity nên không đặt ở Domain — chi tiết implementation của auth, sống
// cùng chỗ với ApplicationUser (cũng ở Infrastructure). Port nguyên từ CRM, không đổi gì.
public class RefreshToken
{
    public int Id { get; set; }

    // Lưu hash (SHA-256) của token, không lưu plain text — nếu DB bị lộ, kẻ tấn công
    // không lấy được token thật để giả làm user.
    public required string TokenHash { get; set; }

    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    // null = còn hiệu lực. Có giá trị = đã bị rotate (dùng để lấy access token mới) hoặc logout.
    public DateTime? RevokedAtUtc { get; set; }

    // Hash của token đã thay thế token này sau khi rotate — dùng để dò chain khi phát hiện
    // reuse (ai đó dùng lại 1 token đã bị revoke).
    public string? ReplacedByTokenHash { get; set; }
}
