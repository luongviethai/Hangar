namespace Hangar.Domain.Entities;

public class Organization
{
    public int Id { get; set; }
    public required string Name { get; set; }

    // Dùng trong URL (ví dụ /o/acme-corp/apps) thay vì Id số — dễ đọc, không lộ thứ tự tạo.
    public required string Slug { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
}
