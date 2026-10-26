using Hangar.Application.Organizations.Dtos;

namespace Hangar.Application.Organizations;

public record CreateOrganizationResult(bool Succeeded, OrganizationDto? Organization, string? Error);
