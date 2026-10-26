using FluentValidation;
using Hangar.Application.Auth;
using Hangar.Application.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hangar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshRequest> _refreshValidator;

    public AuthController(
        IIdentityService identityService,
        ITokenService tokenService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshRequest> refreshValidator)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var validationResult = await _registerValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var result = await _identityService.RegisterAsync(request.Email, request.Password, request.FullName);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        return StatusCode(StatusCodes.Status201Created, new { userId = result.UserId });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var validationResult = await _loginValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var credentials = await _identityService.ValidateCredentialsAsync(request.Email, request.Password);
        if (credentials is null)
        {
            return Unauthorized();
        }

        var accessToken = _tokenService.GenerateAccessToken(credentials);
        var refreshToken = await _identityService.IssueRefreshTokenAsync(credentials.UserId);

        return Ok(new AuthResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refreshToken.Value,
            refreshToken.ExpiresAtUtc,
            credentials.Email,
            credentials.FullName));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var validationResult = await _refreshValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var result = await _identityService.RefreshAsync(request.RefreshToken);
        if (!result.Succeeded || result.User is null || result.RefreshToken is null)
        {
            return Unauthorized();
        }

        var accessToken = _tokenService.GenerateAccessToken(result.User);

        return Ok(new AuthResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            result.RefreshToken.Value,
            result.RefreshToken.ExpiresAtUtc,
            result.User.Email,
            result.User.FullName));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        await _identityService.RevokeRefreshTokenAsync(request.RefreshToken);
        return NoContent();
    }

    // Endpoint tạm để xác nhận JWT hoạt động đúng — xem claims thật trong token, kỳ vọng
    // KHÔNG có claim "role" nào (khác CRM).
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        return Ok(claims);
    }
}
