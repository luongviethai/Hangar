namespace Hangar.Application.Apps.Dtos;

public record AppDto(int Id, int OrganizationId, string Name, string Platform, string PackageId, DateTime CreatedAtUtc);
