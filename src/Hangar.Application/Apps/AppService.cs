using Hangar.Application.Apps.Dtos;
using Hangar.Application.Common.Interfaces;
using Hangar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hangar.Application.Apps;

public class AppService : IAppService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentOrganizationContext _currentOrganizationContext;

    public AppService(IApplicationDbContext context, ICurrentOrganizationContext currentOrganizationContext)
    {
        _context = context;
        _currentOrganizationContext = currentOrganizationContext;
    }

    public async Task<IReadOnlyList<AppDto>> GetAllAsync()
    {
        // Không .Where(OrganizationId == ...) ở đây — global query filter tự lọc đúng
        // Organization hiện tại. Đây chính là lợi ích của Ngày 3: Service không cần nhớ lọc.
        var apps = await _context.Apps.ToListAsync();
        return apps.Select(a => a.ToDto()).ToList();
    }

    public async Task<AppDto?> GetByIdAsync(int id)
    {
        var app = await _context.Apps.FirstOrDefaultAsync(a => a.Id == id);
        return app?.ToDto();
    }

    public async Task<CreateAppResult> CreateAsync(CreateAppRequest request)
    {
        // INSERT không được global query filter giúp gì (filter chỉ áp cho đọc) — phải tự lấy
        // đúng Organization hiện tại để gán vào App mới.
        var organizationId = _currentOrganizationContext.OrganizationId
            ?? throw new InvalidOperationException(
                "Chưa xác định Organization context — action này phải đi qua OrganizationMemberAuthorizationHandler trước.");

        // _context.Apps đã bị filter theo organizationId hiện tại, nên chỉ cần check PackageId,
        // không cần lặp lại điều kiện OrganizationId.
        var packageTaken = await _context.Apps.AnyAsync(a => a.PackageId == request.PackageId);
        if (packageTaken)
        {
            return new CreateAppResult(false, null, $"PackageId '{request.PackageId}' đã tồn tại trong Organization này.");
        }

        var app = new App
        {
            OrganizationId = organizationId,
            Name = request.Name,
            Platform = request.Platform,
            PackageId = request.PackageId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Apps.Add(app);
        await _context.SaveChangesAsync();

        return new CreateAppResult(true, app.ToDto(), null);
    }

    public async Task<AppDto?> UpdateAsync(int id, UpdateAppRequest request)
    {
        var app = await _context.Apps.FirstOrDefaultAsync(a => a.Id == id);
        if (app is null)
        {
            return null;
        }

        app.Name = request.Name;
        app.Platform = request.Platform;
        app.PackageId = request.PackageId;

        await _context.SaveChangesAsync();

        return app.ToDto();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var app = await _context.Apps.FirstOrDefaultAsync(a => a.Id == id);
        if (app is null)
        {
            return false;
        }

        _context.Apps.Remove(app);
        await _context.SaveChangesAsync();

        return true;
    }
}
