namespace Hangar.Application.AppTesters.Dtos;

public record AppTesterDto(int Id, int AppId, int UserId, string Email, string FullName, DateTime InvitedAtUtc);
