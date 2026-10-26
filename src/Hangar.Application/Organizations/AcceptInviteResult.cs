using Hangar.Application.Organizations.Dtos;

namespace Hangar.Application.Organizations;

public record AcceptInviteResult(bool Succeeded, OrganizationDto? Organization, string? Error);
