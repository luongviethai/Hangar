using Hangar.Application.Apps.Dtos;

namespace Hangar.Application.Apps;

public record CreateAppResult(bool Succeeded, AppDto? App, string? Error);
