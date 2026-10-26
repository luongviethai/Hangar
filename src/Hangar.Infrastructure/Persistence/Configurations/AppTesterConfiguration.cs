using Hangar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hangar.Infrastructure.Persistence.Configurations;

public class AppTesterConfiguration : IEntityTypeConfiguration<AppTester>
{
    public void Configure(EntityTypeBuilder<AppTester> builder)
    {
        builder.HasIndex(t => new { t.AppId, t.UserId })
               .IsUnique();

        builder.HasOne<App>()
               .WithMany()
               .HasForeignKey(t => t.AppId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Identity.ApplicationUser>()
               .WithMany()
               .HasForeignKey(t => t.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
