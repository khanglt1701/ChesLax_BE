using System.Net;
using System.Net.Http.Json;
using Cheslax.Domain;
using Cheslax.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
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
    public async Task Authenticated_user_starts_ranked_ai_match_immediately()
    {
        var client = CreateClient();
        await Register(client, "ai_now");
        var enqueue = await client.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" });
        Assert.True(enqueue.IsSuccessStatusCode, await enqueue.Content.ReadAsStringAsync());
        var status = await enqueue.Content.ReadFromJsonAsync<QueueDto>();
        Assert.NotNull(status);
        Assert.Equal("matched", status.Status);
        Assert.Equal("AIOnly", status.MatchmakingMode);
        Assert.Equal("ai", status.OpponentType);
        Assert.False(string.IsNullOrEmpty(status.Match?.RoomCode));
        Assert.Equal("ai", status.Match?.OpponentType);
        Assert.Equal("normal", status.Match?.AiDifficulty);

        var room = await client.GetFromJsonAsync<RoomDto>($"/api/rooms/{status.Match!.RoomCode}");
        Assert.Equal("ranked", room?.Mode);
        Assert.Equal("ai", room?.OpponentType);
        Assert.Contains(room!.Seats, seat => seat.IsAi);
        Assert.DoesNotContain(room.Seats, seat => !seat.IsAi && seat.Name.Contains("Máy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Server_picks_ai_difficulty_from_game_elo()
    {
        var chess = CreateClient();
        var chessName = await Register(chess, "diff_chess");
        SetRating(chessName, GameType.Chess, 750);
        SetRating(chessName, GameType.OAnQuan, 1650);

        var chessQueue = await (await chess.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" }))
            .Content.ReadFromJsonAsync<QueueDto>();
        Assert.Equal("beginner", chessQueue?.Match?.AiDifficulty);

        var oaq = CreateClient();
        var oaqName = await Register(oaq, "diff_oaq");
        SetRating(oaqName, GameType.Chess, 750);
        SetRating(oaqName, GameType.OAnQuan, 1650);
        var oaqQueue = await (await oaq.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "o-an-quan" }))
            .Content.ReadFromJsonAsync<QueueDto>();
        Assert.Equal("advanced", oaqQueue?.Match?.AiDifficulty);
        Assert.NotEqual(chessQueue?.Match?.AiDifficulty, oaqQueue?.Match?.AiDifficulty);
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
    public async Task Ranked_ai_completion_updates_only_that_game_once()
    {
        var client = CreateClient();
        await Register(client, "elo_ai");
        var enqueue = await client.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" });
        var status = await enqueue.Content.ReadFromJsonAsync<QueueDto>();
        var code = status!.Match!.RoomCode;

        var stranger = CreateClient();
        await Register(stranger, "stranger");
        var forged = await stranger.PostAsJsonAsync($"/api/rooms/{code}/resign", new { });
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);

        var room = await client.GetFromJsonAsync<RoomDto>($"/api/rooms/{code}");
        var illegal = await client.PostAsJsonAsync(
            $"/api/rooms/{code}/chess/moves",
            new { from = "e2", to = "e5", version = room?.Version ?? 0 });
        Assert.True(
            illegal.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            await illegal.Content.ReadAsStringAsync());

        var resign = await client.PostAsJsonAsync($"/api/rooms/{code}/resign", new { });
        var finished = await resign.Content.ReadFromJsonAsync<RoomDto>();
        Assert.True(finished?.Rated);
        Assert.Equal("ai", finished?.OpponentType);
        Assert.Equal("ai", finished?.Result?.OpponentType);
        Assert.NotNull(finished?.Result?.YourChange);
        Assert.Equal("loss", finished!.Result!.YourChange!.Result);
        var expected = EloCalculator.Compute(1000, 1100, MatchResultKind.Loss);
        Assert.Equal(expected.NewRating, finished.Result.YourChange.NewRating);
        Assert.Equal(expected.Delta, finished.Result.YourChange.Delta);
        Assert.Equal(AiOpponentPersona.Resolve("normal").Name, finished.Result.YourChange.OpponentName);

        var again = await client.PostAsJsonAsync($"/api/rooms/{code}/resign", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var profile = await client.GetFromJsonAsync<ProfileDto>("/api/users/me");
        Assert.Contains(profile!.Ratings, rating => rating.GameId == "chess" && rating.Rating == expected.NewRating && rating.GamesPlayed == 1);
        Assert.Contains(profile.Ratings, rating => rating.GameId == "o-an-quan" && rating.Rating == 1000 && rating.GamesPlayed == 0);
        _ = room;
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

    private void SetRating(string username, GameType game, int rating)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CheslaxDbContext>();
        var user = db.Users.Single(item => item.UserName == username);
        var row = db.PlayerRatings.Single(item => item.UserId == user.Id && item.GameType == game);
        row.Rating = rating;
        db.SaveChanges();
    }

    private sealed record RoomDto(
        string Code,
        string Game,
        string Mode,
        bool Rated,
        string Status,
        int Version,
        string? OpponentType,
        string? AiDifficulty,
        RoomResultDto? Result,
        List<SeatDto> Seats);

    private sealed record SeatDto(string Name, bool IsAi);

    private sealed record RoomResultDto(string Reason, bool Rated, string? OpponentType, RatingChangeDto? YourChange);

    private sealed record RatingChangeDto(string Result, int NewRating, int Delta, string OpponentName);

    private sealed record QueueDto(string Status, string? MatchmakingMode, string? OpponentType, MatchDto? Match);

    private sealed record MatchDto(string RoomCode, string? OpponentType, string? AiDifficulty);

    private sealed record ProfileDto(string Username, List<RatingDto> Ratings);

    private sealed record RatingDto(string GameId, int Rating, int GamesPlayed);
}

public class HumanRankedMatchmakingTests : IClassFixture<HumanRankedApiFactory>
{
    private readonly HumanRankedApiFactory _factory;

    public HumanRankedMatchmakingTests(HumanRankedApiFactory factory) => _factory = factory;

    [Fact]
    public async Task HumanOnly_mode_queues_instead_of_creating_ai()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await Register(client, "human_only");
        var enqueue = await client.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" });
        Assert.Equal(HttpStatusCode.OK, enqueue.StatusCode);
        var status = await enqueue.Content.ReadFromJsonAsync<QueueDto>();
        Assert.Equal("searching", status?.Status);
        Assert.Equal("HumanOnly", status?.MatchmakingMode);
        Assert.Null(status?.Match);

        var cancel = await client.DeleteAsync("/api/matchmaking/ranked");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/matchmaking/ranked")).StatusCode);
    }

    [Fact]
    public async Task Authenticated_users_are_matched_by_elo()
    {
        var a = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var b = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await Register(a, "rank_a");
        await Register(b, "rank_b");
        Assert.Equal(HttpStatusCode.OK, (await a.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" })).StatusCode);
        var second = await b.PostAsJsonAsync("/api/matchmaking/ranked", new { gameId = "chess" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var status = await b.GetFromJsonAsync<QueueDto>("/api/matchmaking/ranked");
        Assert.Equal("matched", status?.Status);
        Assert.Equal("human", status?.Match?.OpponentType);
        Assert.False(string.IsNullOrEmpty(status?.Match?.RoomCode));
    }

    private static async Task Register(HttpClient client, string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"{prefix}_{suffix}",
            displayName = prefix,
            email = $"{prefix}_{suffix}@example.com",
            password = "correct-horse",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private sealed record QueueDto(string Status, string? MatchmakingMode, MatchDto? Match);

    private sealed record MatchDto(string RoomCode, string? OpponentType);
}

public sealed class HumanRankedApiFactory : CheslaxApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Matchmaking:EnableHumanRankedMatchmaking", "true");
        builder.UseSetting("Matchmaking:RankedMatchmakingMode", "HumanOnly");
    }
}
