using Cheslax.Domain;
using Cheslax.Infrastructure.Options;
using Cheslax.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cheslax.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class HealthController(
    HealthCheckService healthChecks,
    CheslaxDbContext db,
    DatabaseOptions database) : ControllerBase
{
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        var body = new HealthResponse(
            Ok: report.Status == HealthStatus.Healthy && canConnect,
            Status: report.Status.ToString(),
            DatabaseProvider: database.Provider,
            Database: canConnect ? "up" : "down",
            RankCount: RankCatalog.All.Count,
            StartingRating: RankCatalog.StartingRating);

        return report.Status == HealthStatus.Healthy && canConnect
            ? Ok(body)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}

public sealed record HealthResponse(
    bool Ok,
    string Status,
    string DatabaseProvider,
    string Database,
    int RankCount,
    int StartingRating);
