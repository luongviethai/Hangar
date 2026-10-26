using Hangar.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hangar.Infrastructure.Persistence.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.Property(t => t.TokenHash)
                   .IsRequired()
                   .HasMaxLength(256);

            builder.HasIndex(t => t.TokenHash)
                   .IsUnique();

            builder.Property(t => t.ReplacedByTokenHash)
                   .HasMaxLength(256);

            builder.HasOne(t => t.User)
                   .WithMany()
                   .HasForeignKey(t => t.UserId)
                   .OnDelete(DeleteBehavior.Cascade); // xóa user thì refresh token của họ không còn ý nghĩa gì
        }
    }
}
