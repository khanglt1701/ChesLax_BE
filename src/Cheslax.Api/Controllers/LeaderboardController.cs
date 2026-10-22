using Cheslax.Application.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Cheslax.Api.Controllers;

[ApiController]
[Route("api/leaderboard")]
public sealed class LeaderboardController(ILeaderboardService leaderboard) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<LeaderboardDto>> Get(
        [FromQuery] string game = "chess",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 8,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Ok(await leaderboard.GetAsync(game, page, pageSize, userId, cancellationToken));
    }
}
