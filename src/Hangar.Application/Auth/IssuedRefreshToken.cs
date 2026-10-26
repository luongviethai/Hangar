namespace Hangar.Application.Auth;

public record IssuedRefreshToken(string Value, DateTime ExpiresAtUtc);
