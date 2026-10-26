using Hangar.Domain.Entities;

namespace Hangar.Application.Apps.Dtos;

public record UpdateAppRequest(string Name, AppPlatform Platform, string PackageId);
