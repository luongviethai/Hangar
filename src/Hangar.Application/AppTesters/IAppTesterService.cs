using Hangar.Application.AppTesters.Dtos;

namespace Hangar.Application.AppTesters;

public interface IAppTesterService
{
    // null = App không tồn tại (hoặc thuộc Organization khác — global query filter tự chặn).
    Task<IReadOnlyList<AppTesterDto>?> GetTestersAsync(int appId);
    Task<InviteTesterResult?> InviteAsync(int appId, InviteTesterRequest request);
    Task<bool?> RemoveAsync(int appId, int userId);
}
