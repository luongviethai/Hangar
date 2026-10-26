using Hangar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hangar.Infrastructure.Persistence.Configurations;

public class OrganizationInviteConfiguration : IEntityTypeConfiguration<OrganizationInvite>
{
    public void Configure(EntityTypeBuilder<OrganizationInvite> builder)
    {
        builder.Property(i => i.Email)
               .IsRequired()
               .HasMaxLength(256);

        builder.Property(i => i.TokenHash)
               .IsRequired()
               .HasMaxLength(256);

        builder.HasIndex(i => i.TokenHash)
               .IsUnique();

        builder.HasOne(i => i.Organization)
               .WithMany()
               .HasForeignKey(i => i.OrganizationId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
