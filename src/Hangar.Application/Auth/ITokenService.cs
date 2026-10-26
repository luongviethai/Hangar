namespace Hangar.Application.Auth;

public interface ITokenService
{
    AccessToken GenerateAccessToken(UserCredentials user);
}
