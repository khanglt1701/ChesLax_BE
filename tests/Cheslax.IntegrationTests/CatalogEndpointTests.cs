using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cheslax.IntegrationTests;

public class CatalogEndpointTests : IClassFixture<CheslaxApiFactory>
{
    private readonly HttpClient _client;

    public CatalogEndpointTests(CheslaxApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
    }

    [Fact]
    public async Task Meta_exposes_ranks_and_time_controls()
    {
        var ranks = await _client.GetFromJsonAsync<RankDto[]>("/api/meta/ranks");
        Assert.NotNull(ranks);
        Assert.Equal(15, ranks.Length);
        Assert.Equal("tap-su", ranks[0].Id);
        Assert.Equal("ky-thanh", ranks[^1].Id);

        var controls = await _client.GetFromJsonAsync<TimeControlDto[]>("/api/meta/time-controls");
        Assert.NotNull(controls);
        Assert.Contains(controls, item => item.Id == "off" && item.BaseSeconds == 0);
        Assert.Contains(controls, item => item.Id == "5+0" && item.BaseSeconds == 300);
        Assert.Contains(controls, item => item.Id == "3+2" && item.IncrementSeconds == 2);
    }

    [Fact]
    public async Task Leaderboard_and_preferences_round_trip()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"board_{suffix}",
            displayName = "Bảng Test",
            email = $"board_{suffix}@example.com",
            password = "correct-horse",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var board = await _client.GetFromJsonAsync<LeaderboardDto>("/api/leaderboard?game=chess");
        Assert.NotNull(board);
        Assert.Equal("chess", board.GameId);
        Assert.True(board.TotalCount >= 1);
        Assert.NotNull(board.You);
        Assert.Equal($"board_{suffix}", board.You.Username);
        Assert.Equal(1000, board.You.Rating);

        var prefs = await _client.GetFromJsonAsync<PreferenceDto>("/api/users/me/preferences");
        Assert.NotNull(prefs);
        Assert.Equal("light", prefs.Theme);
        Assert.Equal("wood", prefs.ChessBoardTheme);

        var updated = await _client.PutAsJsonAsync("/api/users/me/preferences", new
        {
            theme = "dark",
            chessBoardTheme = "marble",
            chessPieceSet = "classic",
            oAnQuanTheme = "stone",
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var saved = await updated.Content.ReadFromJsonAsync<PreferenceDto>();
        Assert.NotNull(saved);
        Assert.Equal("dark", saved.Theme);
        Assert.Equal("marble", saved.ChessBoardTheme);
        Assert.Equal("classic", saved.ChessPieceSet);
        Assert.Equal("stone", saved.OAnQuanTheme);
    }

    [Fact]
    public async Task Leaderboard_rejects_unknown_game()
    {
        var response = await _client.GetAsync("/api/leaderboard?game=sudoku");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record RankDto(string Id, int Level, string Name, int MinRating, int? MaxRating);

    private sealed record TimeControlDto(string Id, string Label, int BaseSeconds, int IncrementSeconds);

    private sealed record LeaderboardDto(
        string GameId,
        List<LeaderboardEntryDto> Items,
        int Page,
        int PageSize,
        int TotalCount,
        LeaderboardEntryDto? You);

    private sealed record LeaderboardEntryDto(
        int Rank,
        string Username,
        string DisplayName,
        string AvatarId,
        int Rating,
        int GamesPlayed);

    private sealed record PreferenceDto(
        string Theme,
        string ChessBoardTheme,
        string ChessPieceSet,
        string OAnQuanTheme,
        DateTimeOffset UpdatedAt);
}
