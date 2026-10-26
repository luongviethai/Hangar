using System.Security.Cryptography;
using System.Text;
using Hangar.Application.Auth;
using Hangar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Hangar.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;
    private readonly JwtSettings _settings;

    public IdentityService(UserManager<ApplicationUser> userManager, AppDbContext context, IOptions<JwtSettings> options)
    {
        _userManager = userManager;
        _context = context;
        _settings = options.Value;
    }

    public async Task<RegisterResult> RegisterAsync(string email, string password, string fullName)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName
        };

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            return new RegisterResult(false, null, result.Errors.Select(e => e.Description).ToList());
        }

        // Khác CRM: không AddToRoleAsync gì ở đây — Hangar không có role toàn cục để gán lúc
        // đăng ký. Vai trò (Owner/Developer) chỉ xuất hiện khi user tạo/được mời vào 1
        // Organization (OrganizationMember, xem Ngày 2).
        return new RegisterResult(true, user.Id, []);
    }

    public async Task<UserCredentials?> ValidateCredentialsAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return null;
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            return null;
        }

        return new UserCredentials(user.Id, user.Email!, user.FullName);
    }

    public async Task<IssuedRefreshToken> IssueRefreshTokenAsync(int userId)
    {
        var (entity, plainValue) = CreateRefreshTokenEntity(userId);

        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync();

        return new IssuedRefreshToken(plainValue, entity.ExpiresAtUtc);
    }

    public async Task<RefreshResult> RefreshAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        var stored = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (stored is null)
        {
            return new RefreshResult(false, null, null);
        }

        if (stored.RevokedAtUtc is not null)
        {
            // Token này đã bị rotate/revoke trước đó mà vẫn có người dùng lại — chỉ user hợp lệ
            // mới luôn dùng token MỚI nhất, nên đây là dấu hiệu rõ của việc token bị đánh cắp.
            // Phản ứng: revoke toàn bộ refresh token còn sống của user này, buộc login lại từ đầu.
            await RevokeAllActiveTokensForUserAsync(stored.UserId);
            return new RefreshResult(false, null, null);
        }

        if (stored.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return new RefreshResult(false, null, null);
        }

        var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
        {
            return new RefreshResult(false, null, null);
        }

        var (newEntity, newPlainValue) = CreateRefreshTokenEntity(user.Id);

        // Rotate: revoke token cũ + tạo token mới trong CÙNG 1 SaveChangesAsync — EF Core gói
        // mọi thay đổi đang track vào 1 transaction DB, nên không có khoảng hở "cũ đã revoke
        // nhưng mới chưa tồn tại".
        stored.RevokedAtUtc = DateTime.UtcNow;
        stored.ReplacedByTokenHash = newEntity.TokenHash;
        _context.RefreshTokens.Add(newEntity);
        await _context.SaveChangesAsync();

        var credentials = new UserCredentials(user.Id, user.Email!, user.FullName);

        return new RefreshResult(true, credentials, new IssuedRefreshToken(newPlainValue, newEntity.ExpiresAtUtc));
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        var stored = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (stored is not null && stored.RevokedAtUtc is null)
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    private async Task RevokeAllActiveTokensForUserAsync(int userId)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    private (RefreshToken Entity, string PlainValue) CreateRefreshTokenEntity(int userId)
    {
        var plainValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        var entity = new RefreshToken
        {
            TokenHash = Hash(plainValue),
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays)
        };

        return (entity, plainValue);
    }

    // SHA-256 thường là đủ cho refresh token (khác với password): token là random 512-bit do
    // mình tự sinh, không phải thứ con người nghĩ ra nên không có "từ điển" để brute-force dò —
    // không cần thuật toán chậm có salt như PBKDF2/bcrypt dùng cho password.
    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
