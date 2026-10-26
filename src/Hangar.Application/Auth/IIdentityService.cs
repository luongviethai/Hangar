namespace Hangar.Application.Auth;

public interface IIdentityService
{
    Task<RegisterResult> RegisterAsync(string email, string password, string fullName);
    Task<UserCredentials?> ValidateCredentialsAsync(string email, string password);

    Task<IssuedRefreshToken> IssueRefreshTokenAsync(int userId);
    Task<RefreshResult> RefreshAsync(string refreshToken);
    Task RevokeRefreshTokenAsync(string refreshToken);
}
