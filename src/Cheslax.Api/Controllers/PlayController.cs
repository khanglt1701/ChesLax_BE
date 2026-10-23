using System.Security.Claims;
using Cheslax.Application.Auth;
using Cheslax.Application.Play;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cheslax.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class PlayController(
    IPlayService play,
    IMatchmakingService matchmaking,
    IAuthService auth,
    IValidator<CreateCasualRoomRequest> createValidator,
    IValidator<EnqueueRankedRequest> enqueueValidator) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("auth/guest")]
    public async Task<ActionResult<UserDto>> Guest([FromBody] GuestRequest? request, CancellationToken cancellationToken)
    {
        var userId = UserIdOrNull();
        return Ok(await auth.EnsureGuestAsync(request ?? new GuestRequest(null, null), userId, cancellationToken));
    }

    [HttpPost("rooms")]
    [EnableRateLimiting("rooms")]
    public async Task<ActionResult<RoomPublicDto>> CreateRoom(
        [FromBody] CreateCasualRoomRequest request,
        CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var room = await play.CreateCasualRoomAsync(RequireUserId(), request, cancellationToken);
        return Created($"/api/rooms/{room.Code}", room);
    }

    [HttpPost("rooms/{code}/join")]
    [EnableRateLimiting("rooms")]
    public async Task<ActionResult<RoomPublicDto>> Join(string code, CancellationToken cancellationToken) =>
        Ok(await play.JoinRoomAsync(RequireUserId(), code, cancellationToken));

    [HttpGet("rooms/{code}")]
    public async Task<ActionResult<RoomPublicDto>> Get(string code, CancellationToken cancellationToken) =>
        Ok(await play.GetRoomAsync(RequireUserId(), code, cancellationToken));

    [HttpPost("rooms/{code}/leave")]
    public async Task<IActionResult> Leave(string code, CancellationToken cancellationToken)
    {
        await play.LeaveRoomAsync(RequireUserId(), code, cancellationToken);
        return NoContent();
    }

    [HttpPost("rooms/{code}/chess/moves")]
    public async Task<ActionResult<RoomPublicDto>> ChessMove(
        string code,
        [FromBody] ChessMoveRequest request,
        CancellationToken cancellationToken) =>
        Ok(await play.ChessMoveAsync(RequireUserId(), code, request, cancellationToken));

    [HttpPost("rooms/{code}/o-an-quan/moves")]
    public async Task<ActionResult<RoomPublicDto>> OAnQuanMove(
        string code,
        [FromBody] OAnQuanMoveRequest request,
        CancellationToken cancellationToken) =>
        Ok(await play.OAnQuanMoveAsync(RequireUserId(), code, request, cancellationToken));

    [HttpPost("rooms/{code}/resign")]
    public async Task<ActionResult<RoomPublicDto>> Resign(string code, CancellationToken cancellationToken) =>
        Ok(await play.ResignAsync(RequireUserId(), code, cancellationToken));

    [HttpPost("matchmaking/ranked")]
    public async Task<ActionResult<QueueStatusDto>> Enqueue(
        [FromBody] EnqueueRankedRequest request,
        CancellationToken cancellationToken)
    {
        await enqueueValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await matchmaking.EnqueueAsync(RequireUserId(), request.GameId, null, cancellationToken));
    }

    [HttpDelete("matchmaking/ranked")]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken)
    {
        await matchmaking.CancelAsync(RequireUserId(), cancellationToken);
        return NoContent();
    }

    [HttpGet("matchmaking/ranked")]
    public async Task<ActionResult<QueueStatusDto>> Status(CancellationToken cancellationToken)
    {
        var status = await matchmaking.GetStatusAsync(RequireUserId(), cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    private string RequireUserId() =>
        UserIdOrNull() ?? throw new UnauthorizedAccessException();

    private string? UserIdOrNull() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
