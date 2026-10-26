using Hangar.Application.Apps.Dtos;
using Hangar.Domain.Entities;

namespace Hangar.Application.Apps;

public static class AppMappingExtensions
{
    public static AppDto ToDto(this App app)
    {
        return new AppDto(app.Id, app.OrganizationId, app.Name, app.Platform.ToString(), app.PackageId, app.CreatedAtUtc);
    }
}
