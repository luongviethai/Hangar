using Hangar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hangar.Infrastructure.Persistence.Configurations;

public class AppConfiguration : IEntityTypeConfiguration<App>
{
    public void Configure(EntityTypeBuilder<App> builder)
    {
        builder.Property(a => a.Name)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(a => a.PackageId)
               .IsRequired()
               .HasMaxLength(200);

        // PackageId unique trong phạm vi 1 Organization — KHÔNG unique toàn cục (đa tenant).
        builder.HasIndex(a => new { a.OrganizationId, a.PackageId })
               .IsUnique();

        builder.HasOne(a => a.Organization)
               .WithMany()
               .HasForeignKey(a => a.OrganizationId)
               .OnDelete(DeleteBehavior.Cascade); // xóa Organization thì xóa luôn App của nó

        // Global query filter (multi-tenant) KHÔNG đặt ở đây — ApplyConfigurationsFromAssembly()
        // khởi tạo class này bằng Activator.CreateInstance (không qua DI), nên không inject được
        // ICurrentOrganizationContext vào constructor. Filter đặt trực tiếp trong
        // AppDbContext.OnModelCreating, nơi DbContext tự nó ĐÃ được DI khởi tạo đúng cách.
    }
}
