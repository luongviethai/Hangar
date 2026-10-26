using Hangar.Domain.Entities;

namespace Hangar.Application.Organizations.Dtos;

public record InviteMemberRequest(string Email, OrganizationRole Role);
