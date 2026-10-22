using Cheslax.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Cheslax.Api.Controllers;

[ApiController]
[Route("api/meta")]
public sealed class MetaController : ControllerBase
{
    [HttpGet("ranks")]
    public ActionResult<IReadOnlyList<RankDefinition>> Ranks() => Ok(RankCatalog.All);

    [HttpGet("time-controls")]
    public ActionResult<IReadOnlyList<string>> TimeControls() =>
        Ok(TimeControl.Presets.Select(preset => preset.Id).Prepend("off").ToArray());

    [HttpGet("games")]
    public ActionResult<IReadOnlyList<string>> Games() =>
        Ok(new[] { GameTypeIds.Chess, GameTypeIds.OAnQuan });
}
