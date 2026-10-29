using Hangar.Application.Common.Interfaces;
using Hangar.Domain.Entities;
using Hangar.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hangar.Infrastructure.Persistence;

// IdentityUserContext<TUser,TKey> — KHÔNG phải IdentityDbContext<TUser,TRole,TKey> như CRM.
// Lớp cha này chỉ có AspNetUsers/UserClaims/UserLogins/UserTokens, không có AspNetRoles/
// AspNetUserRoles — khớp với AddIdentityCore() ở DependencyInjection.cs (không đăng ký Role).
public class AppDbContext : IdentityUserContext<ApplicationUser, int>, IApplicationDbContext
{
    private readonly ICurrentOrganizationContext _currentOrganizationContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentOrganizationContext currentOrganizationContext)
        : base(options)
    {
        _currentOrganizationContext = currentOrganizationContext;
    }

    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<OrganizationMember> OrganizationMembers { get; set; }
    public DbSet<App> Apps { get; set; }
    public DbSet<AppTester> AppTesters { get; set; }
    public DbSet<OrganizationInvite> OrganizationInvites { get; set; }
    public DbSet<Build> Builds { get; set; }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global query filter cho multi-tenant. Đặt ở đây (không phải trong AppConfiguration)
        // vì AppDbContext được chính DI container khởi tạo (constructor injection hoạt động
        // đúng), nên field _currentOrganizationContext luôn là giá trị scoped đúng của request
        // hiện tại. Khi chưa set (null, mặc định) -> a.OrganizationId == null luôn false với
        // mọi App thật -> query trả về rỗng, không lọt dữ liệu ra ngoài khi quên set context.
        builder.Entity<App>().HasQueryFilter(a => a.OrganizationId == _currentOrganizationContext.OrganizationId);
    }
}
