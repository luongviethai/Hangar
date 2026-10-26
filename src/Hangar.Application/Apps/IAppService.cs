using Hangar.Application.Apps.Dtos;

namespace Hangar.Application.Apps;

public interface IAppService
{
    // Không nhận organizationId làm tham số — global query filter (AppDbContext) đã tự lọc
    // đúng Organization hiện tại, set sẵn bởi OrganizationMemberAuthorizationHandler.
    Task<IReadOnlyList<AppDto>> GetAllAsync();
    Task<AppDto?> GetByIdAsync(int id);
    Task<CreateAppResult> CreateAsync(CreateAppRequest request);
    Task<AppDto?> UpdateAsync(int id, UpdateAppRequest request);
    Task<bool> DeleteAsync(int id);
}
