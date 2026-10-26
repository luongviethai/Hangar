using Hangar.Application.Common.Interfaces;

namespace Hangar.Infrastructure.Persistence;

public class CurrentOrganizationContext : ICurrentOrganizationContext
{
    public int? OrganizationId { get; set; }
}
