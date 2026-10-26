namespace Hangar.Application.Auth;

public record RefreshResult(bool Succeeded, UserCredentials? User, IssuedRefreshToken? RefreshToken);
