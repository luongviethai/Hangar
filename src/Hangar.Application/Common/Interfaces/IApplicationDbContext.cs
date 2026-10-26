using Hangar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hangar.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Organization> Organizations { get; set; }
    DbSet<OrganizationMember> OrganizationMembers { get; set; }
    DbSet<App> Apps { get; set; }
    DbSet<AppTester> AppTesters { get; set; }
    DbSet<OrganizationInvite> OrganizationInvites { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
