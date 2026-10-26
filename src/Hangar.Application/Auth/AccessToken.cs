namespace Hangar.Application.Auth;

public record AccessToken(string Value, DateTime ExpiresAtUtc);
