namespace Hangar.Application.Auth;

public record RegisterResult(bool Succeeded, int? UserId, IReadOnlyList<string> Errors);
