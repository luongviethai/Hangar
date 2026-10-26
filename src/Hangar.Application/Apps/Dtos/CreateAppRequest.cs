using Hangar.Domain.Entities;

namespace Hangar.Application.Apps.Dtos;

public record CreateAppRequest(string Name, AppPlatform Platform, string PackageId);
