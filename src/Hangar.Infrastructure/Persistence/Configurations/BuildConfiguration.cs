using Hangar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hangar.Infrastructure.Persistence.Configurations;

public class BuildConfiguration : IEntityTypeConfiguration<Build>
{
    public void Configure(EntityTypeBuilder<Build> builder)
    {
        builder.HasIndex(b => b.AppId);

        builder.HasOne<App>()
               .WithMany()
               .HasForeignKey(b => b.AppId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Identity.ApplicationUser>()
               .WithMany()
               .HasForeignKey(b => b.UploadedByUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
