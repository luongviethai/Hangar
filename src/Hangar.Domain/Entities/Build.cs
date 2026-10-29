namespace Hangar.Domain.Entities;

public class Build
{
    public int Id { get; set; }
    public int AppId { get; set; }          // scalar FK — không navigation, giống AppTester/OrganizationMember
    public string FileName { get; set; } = default!;   // tên file gốc lúc upload
    public string ObjectKey { get; set; } = default!;  // path trong MinIO, ví dụ "apps/3/ab12.../app-release.apk"
    public long SizeBytes { get; set; }
    public string? Version { get; set; }      // null hôm nay — Ngày 12 mới đọc được từ file
    public int? BuildNumber { get; set; }     // null hôm nay — Ngày 12 mới đọc được từ file
    public int UploadedByUserId { get; set; } // scalar FK tới ApplicationUser — không navigation
    public DateTime CreatedAtUtc { get; set; }
}
