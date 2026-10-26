using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hangar.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hangar.Infrastructure.Identity;

public class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
    }

    public AccessToken GenerateAccessToken(UserCredentials user)
    {
        // Khác CRM: không có claim Role — JWT chỉ xác định danh tính (NameIdentifier/Email/
        // Name), không xác định quyền. Quyền phụ thuộc Organization đang truy cập, tra ở
        // tầng authorization (OrganizationMemberAuthorizationHandler, Ngày 4), không nhúng
        // sẵn vào token vì không ổn định suốt đời token.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessToken(value, expiresAtUtc);
    }
}
