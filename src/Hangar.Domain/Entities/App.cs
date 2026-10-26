namespace Hangar.Domain.Entities;

public class App
{
    public int Id { get; set; }

    // Cột multi-tenant — mọi query tới App tự động bị lọc theo field này (global query filter,
    // xem AppDbContext.OnModelCreating).
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public required string Name { get; set; }
    public AppPlatform Platform { get; set; }

    // required string, unique trong phạm vi 1 Organization (không phải toàn cục — 2 Organization
    // khác nhau được đăng ký cùng PackageId, xem AppConfiguration).
    public required string PackageId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
