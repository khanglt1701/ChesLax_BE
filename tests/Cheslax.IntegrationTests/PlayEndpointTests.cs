using System.Net;
using System.Net.Http.Json;
using Cheslax.Domain;
using Cheslax.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cheslax.IntegrationTests;

public class PlayEndpointTests : IClassFixture<CheslaxApiFactory>
{
    private readonly CheslaxApiFactory _factory;

    public PlayEndpointTests(CheslaxApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Guest_can_play_casual_but_not_ranked()
    {
        var client = CreateClient();
        var guest = await client.PostAsJsonAsync("/api/auth/guest", new { displayName = "Khách Test" });
        Assert.Equal(HttpStatusCode.OK, guest.StatusCode);

        var ranked = await client.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" });
        Assert.Equal(HttpStatusCode.Unauthorized, ranked.StatusCode);

        var room = await client.PostAsJsonAsync("/api/rooms", new { gameId = "chess", timeControl = "off", colorPref = "w" });
        Assert.True(room.IsSuccessStatusCode, await room.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Created, room.StatusCode);
        var created = await room.Content.ReadFromJsonAsync<RoomDto>();
        Assert.NotNull(created);
        Assert.Equal("casual", created.Mode);
        Assert.False(created.Rated);
    }

    [Fact]
    public async Task Authenticated_users_are_matched_by_elo_and_cannot_double_queue()
    {
        var a = CreateClient();
        var b = CreateClient();
        await Register(a, "rank_a");
        await Register(b, "rank_b");

        var first = await a.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" });
        Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var duplicate = await a.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var second = await b.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var status = await b.GetFromJsonAsync<QueueDto>("/api/matchmaking/ranked");
        Assert.NotNull(status);
        Assert.Equal("matched", status.Status);
        Assert.False(string.IsNullOrEmpty(status.Match?.RoomCode));

        var far = CreateClient();
        await Register(far, "rank_far");
        var queued = await far.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "o-an-quan" });
        Assert.Equal(HttpStatusCode.OK, queued.StatusCode);
        var oaq = await far.GetFromJsonAsync<QueueDto>("/api/matchmaking/ranked");
        Assert.Equal("searching", oaq?.Status);
        await far.DeleteAsync("/api/matchmaking/ranked");
    }

    [Fact]
    public async Task Players_outside_elo_range_are_not_matched()
    {
        var close = CreateClient();
        var far = CreateClient();
        var closeName = await Register(close, "spread_close");
        var farName = await Register(far, "spread_far");
        SetChessRating(closeName, 1000);
        SetChessRating(farName, 1800);

        Assert.Equal(HttpStatusCode.OK, (await close.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await far.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" })).StatusCode);
        var closeStatus = await close.GetFromJsonAsync<QueueDto>("/api/matchmaking/ranked");
        var farStatus = await far.GetFromJsonAsync<QueueDto>("/api/matchmaking/ranked");
        Assert.Equal("searching", closeStatus?.Status);
        Assert.Equal("searching", farStatus?.Status);

        await close.DeleteAsync("/api/matchmaking/ranked");
        await far.DeleteAsync("/api/matchmaking/ranked");
    }

    [Fact]
    public async Task Casual_result_does_not_change_elo_and_duplicate_resign_is_safe()
    {
        var host = CreateClient();
        var guest = CreateClient();
        await Register(host, "casual_host");
        await guest.PostAsJsonAsync("/api/auth/guest", new { displayName = "Khách" });

        var created = await host.PostAsJsonAsync("/api/rooms", new { gameId = "chess", timeControl = "off" });
        var room = await created.Content.ReadFromJsonAsync<RoomDto>();
        Assert.NotNull(room);
        var joined = await guest.PostAsJsonAsync($"/api/rooms/{room.Code}/join", new { });
        Assert.Equal(HttpStatusCode.OK, joined.StatusCode);

        var resign = await host.PostAsJsonAsync($"/api/rooms/{room.Code}/resign", new { });
        Assert.Equal(HttpStatusCode.OK, resign.StatusCode);
        var finished = await resign.Content.ReadFromJsonAsync<RoomDto>();
        Assert.Equal("ended", finished?.Status);
        Assert.False(finished?.Rated);
        Assert.Null(finished?.Result?.YourChange);

        var again = await host.PostAsJsonAsync($"/api/rooms/{room.Code}/resign", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var me = await host.GetFromJsonAsync<ProfileDto>("/api/users/me");
        Assert.Contains(me!.Ratings, rating => rating.GameId == "chess" && rating.Rating == 1000 && rating.GamesPlayed == 0);
    }

    [Fact]
    public async Task Ranked_completion_updates_only_that_game_once()
    {
        var a = CreateClient();
        var b = CreateClient();
        await Register(a, "elo_a");
        await Register(b, "elo_b");
        await a.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" });
        await b.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" });
        var status = await a.GetFromJsonAsync<QueueDto>("/api/matchmaking/ranked");
        var code = status!.Match!.RoomCode;

        var stranger = CreateClient();
        await Register(stranger, "stranger");
        var forged = await stranger.PostAsJsonAsync($"/api/rooms/{code}/resign", new { });
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);

        var resign = await a.PostAsJsonAsync($"/api/rooms/{code}/resign", new { });
        var finished = await resign.Content.ReadFromJsonAsync<RoomDto>();
        Assert.True(finished?.Rated);
        Assert.NotNull(finished?.Result?.YourChange);
        Assert.Equal("loss", finished!.Result!.YourChange!.Result);
        Assert.Equal(984, finished.Result.YourChange.NewRating);

        var again = await a.PostAsJsonAsync($"/api/rooms/{code}/resign", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var profile = await a.GetFromJsonAsync<ProfileDto>("/api/users/me");
        Assert.Contains(profile!.Ratings, rating => rating.GameId == "chess" && rating.Rating == 984 && rating.GamesPlayed == 1);
        Assert.Contains(profile.Ratings, rating => rating.GameId == "o-an-quan" && rating.Rating == 1000 && rating.GamesPlayed == 0);
    }

    [Fact]
    public async Task Cancel_removes_player_from_queue()
    {
        var client = CreateClient();
        await Register(client, "cancel_me");
        await client.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" });
        var cancel = await client.DeleteAsync("/api/matchmaking/ranked");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        var status = await client.GetAsync("/api/matchmaking/ranked");
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    private static async Task<string> Register(HttpClient client, string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"{prefix}_{suffix}";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username,
            displayName = prefix,
            email = $"{prefix}_{suffix}@example.com",
            password = "correct-horse",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return username;
    }

    private void SetChessRating(string username, int rating)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CheslaxDbContext>();
        var user = db.Users.Single(item => item.UserName == username);
        var row = db.PlayerRatings.Single(item => item.UserId == user.Id && item.GameType == GameType.Chess);
        row.Rating = rating;
        db.SaveChanges();
    }

    private sealed record RoomDto(
        string Code,
        string Game,
        string Mode,
        bool Rated,
        string Status,
        RoomResultDto? Result);

    private sealed record RoomResultDto(string Reason, bool Rated, RatingChangeDto? YourChange);

    private sealed record RatingChangeDto(string Result, int NewRating);

    private sealed record QueueDto(string Status, MatchDto? Match);

    private sealed record MatchDto(string RoomCode);

    private sealed record ProfileDto(string Username, List<RatingDto> Ratings);

    private sealed record RatingDto(string GameId, int Rating, int GamesPlayed);
}
