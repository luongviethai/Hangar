using Hangar.Application.AppTesters;
using Hangar.Application.AppTesters.Dtos;
using Hangar.Domain.Entities;
using Hangar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hangar.Infrastructure.Identity;

// Sống ở Infrastructure (không phải Application như AppService) vì cần JOIN với ApplicationUser
// (Email/FullName) để trả về — IApplicationDbContext (ở Application) không được phép biết tới
// ApplicationUser (Domain/Application không phụ thuộc Infrastructure). Giống lý do
// UserManagementService/RoleManagementService sống ở Infrastructure trong CRM.
public class AppTesterService : IAppTesterService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AppTesterService(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<AppTesterDto>?> GetTestersAsync(int appId)
    {
        var appExists = await _context.Apps.AnyAsync(a => a.Id == appId);
        if (!appExists)
        {
            return null;
        }

        return await (
            from t in _context.AppTesters
            join u in _context.Users on t.UserId equals u.Id
            where t.AppId == appId
            select new AppTesterDto(t.Id, t.AppId, t.UserId, u.Email!, u.FullName, t.InvitedAtUtc)
        ).ToListAsync();
    }

    public async Task<InviteTesterResult?> InviteAsync(int appId, InviteTesterRequest request)
    {
        var appExists = await _context.Apps.AnyAsync(a => a.Id == appId);
        if (!appExists)
        {
            return null;
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return new InviteTesterResult(
                false, null,
                $"Không tìm thấy user với email '{request.Email}' — họ cần đăng ký tài khoản Hangar trước.");
        }

        var existing = await _context.AppTesters
            .FirstOrDefaultAsync(t => t.AppId == appId && t.UserId == user.Id);

        if (existing is not null)
        {
            // idempotent — mời lại người đã được mời không phải lỗi.
            return new InviteTesterResult(true, ToDto(existing, user), null);
        }

        var tester = new AppTester
        {
            AppId = appId,
            UserId = user.Id,
            InvitedAtUtc = DateTime.UtcNow
        };

        _context.AppTesters.Add(tester);
        await _context.SaveChangesAsync();

        return new InviteTesterResult(true, ToDto(tester, user), null);
    }

    public async Task<bool?> RemoveAsync(int appId, int userId)
    {
        var appExists = await _context.Apps.AnyAsync(a => a.Id == appId);
        if (!appExists)
        {
            return null;
        }

        var tester = await _context.AppTesters
            .FirstOrDefaultAsync(t => t.AppId == appId && t.UserId == userId);

        if (tester is null)
        {
            return false;
        }

        _context.AppTesters.Remove(tester);
        await _context.SaveChangesAsync();

        return true;
    }

    private static AppTesterDto ToDto(AppTester tester, ApplicationUser user)
        => new(tester.Id, tester.AppId, tester.UserId, user.Email!, user.FullName, tester.InvitedAtUtc);
}
