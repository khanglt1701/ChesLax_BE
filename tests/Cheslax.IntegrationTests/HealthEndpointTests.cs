using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cheslax.IntegrationTests;

public class HealthEndpointTests : IClassFixture<CheslaxApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(CheslaxApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_returns_ok_and_sqlite_provider()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthDto>();
        Assert.NotNull(body);
        Assert.True(body.Ok);
        Assert.Equal("Sqlite", body.DatabaseProvider);
        Assert.Equal("up", body.Database);
        Assert.Equal(15, body.RankCount);
        Assert.Equal(1000, body.StartingRating);
    }

    [Fact]
    public async Task Meta_exposes_game_ids()
    {
        var games = await _client.GetFromJsonAsync<string[]>("/api/meta/games");
        Assert.NotNull(games);
        Assert.Equal(new[] { "chess", "o-an-quan" }, games);
    }

    private sealed record HealthDto(
        bool Ok,
        string Status,
        string DatabaseProvider,
        string Database,
        int RankCount,
        int StartingRating);
}

public sealed class CheslaxApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"cheslax-it-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting("Cors:Origins:0", "http://localhost:5173");
        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of the throwaway SQLite file.
        }
    }
}
