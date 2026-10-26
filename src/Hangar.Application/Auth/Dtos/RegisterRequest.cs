namespace Hangar.Application.Auth.Dtos;

public record RegisterRequest(string Email, string Password, string FullName);
