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
    public ActionResult<IReadOnlyList<TimeControlDto>> TimeControls() => Ok(
        TimeControl.Presets
            .Select(preset => new TimeControlDto(preset.Id, preset.Label, preset.BaseSeconds, preset.IncrementSeconds))
            .Prepend(new TimeControlDto(TimeControl.Off.Id, TimeControl.Off.Label, 0, 0))
            .ToArray());

    [HttpGet("games")]
    public ActionResult<IReadOnlyList<string>> Games() =>
        Ok(new[] { GameTypeIds.Chess, GameTypeIds.OAnQuan });
}

public sealed record TimeControlDto(string Id, string Label, int BaseSeconds, int IncrementSeconds);
