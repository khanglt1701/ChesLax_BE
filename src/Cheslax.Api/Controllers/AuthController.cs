using Cheslax.Application.Auth;
using Cheslax.Application.Catalog;
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
public sealed class UsersController(
    IAuthService auth,
    IPreferenceService preferences,
    IValidator<UpdatePreferenceRequest> preferenceValidator) : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<ProfileDto>> Me(CancellationToken cancellationToken)
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
    public async Task<ActionResult<ProfileDto>> Update([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        return Ok(await auth.UpdateCurrentAsync(userId, request, cancellationToken));
    }

    [Authorize]
    [HttpGet("me/preferences")]
    public async Task<ActionResult<UserPreferenceDto>> Preferences(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        return Ok(await preferences.GetAsync(userId, cancellationToken));
    }

    [Authorize]
    [HttpPut("me/preferences")]
    public async Task<ActionResult<UserPreferenceDto>> UpdatePreferences(
        [FromBody] UpdatePreferenceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        await preferenceValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await preferences.UpdateAsync(userId, request, cancellationToken));
    }

    [HttpGet("{username}")]
    public async Task<ActionResult<ProfileDto>> PublicProfile(string username, CancellationToken cancellationToken)
    {
        var profile = await auth.GetPublicAsync(username, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }
}
