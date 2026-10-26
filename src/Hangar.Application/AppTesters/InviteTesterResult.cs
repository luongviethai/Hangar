using Hangar.Application.AppTesters.Dtos;

namespace Hangar.Application.AppTesters;

public record InviteTesterResult(bool Succeeded, AppTesterDto? Tester, string? Error);
