using Hangar.Application.Organizations.Dtos;
using Hangar.Domain.Entities;

namespace Hangar.Application.Organizations;

public static class OrganizationMappingExtensions
{
    public static OrganizationDto ToDto(this Organization organization, OrganizationRole myRole)
    {
        return new OrganizationDto(organization.Id, organization.Name, organization.Slug, myRole.ToString());
    }
}
