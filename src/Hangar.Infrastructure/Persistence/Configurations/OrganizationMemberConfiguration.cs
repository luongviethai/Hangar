using Hangar.Domain.Entities;
using Hangar.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hangar.Infrastructure.Persistence.Configurations;

public class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        // 1 user chỉ có đúng 1 vai trò trong 1 Organization — không trùng dòng.
        builder.HasIndex(m => new { m.OrganizationId, m.UserId })
               .IsUnique();

        builder.HasOne(m => m.Organization)
               .WithMany(o => o.Members)
               .HasForeignKey(m => m.OrganizationId)
               .OnDelete(DeleteBehavior.Cascade); // xóa Organization thì xóa luôn membership của nó

        // OrganizationMember -> ApplicationUser: chỉ FK, không navigation property (xem ghi chú
        // ở OrganizationMember.cs) — giống cách Order.CreatedBy cấu hình ở CRM.
        builder.HasOne<ApplicationUser>()
               .WithMany()
               .HasForeignKey(m => m.UserId)
               .OnDelete(DeleteBehavior.Cascade); // user bị xóa thì membership của họ không còn ý nghĩa
    }
}
