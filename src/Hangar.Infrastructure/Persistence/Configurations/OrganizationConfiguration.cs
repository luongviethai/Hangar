using Hangar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hangar.Infrastructure.Persistence.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.Property(o => o.Name)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(o => o.Slug)
               .IsRequired()
               .HasMaxLength(100);

        builder.HasIndex(o => o.Slug)
               .IsUnique();
    }
}
