using Cheslax.Application.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cheslax.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth, IValidator<RegisterRequest> registerValidator, IValidator<LoginRequest> loginValidator)
    : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await auth.RegisterAsync(request, cancellationToken);
        return Created("/api/users/me", user);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<UserDto>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await auth.LoginAsync(request, cancellationToken));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/users")]
public sealed class UsersController(IAuthService auth) : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        return Ok(await auth.GetCurrentAsync(userId, cancellationToken));
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> Update([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        return Ok(await auth.UpdateCurrentAsync(userId, request, cancellationToken));
    }

    [HttpGet("{username}")]
    public async Task<ActionResult<PublicProfileDto>> PublicProfile(string username, CancellationToken cancellationToken)
    {
        var profile = await auth.GetPublicAsync(username, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }
}
