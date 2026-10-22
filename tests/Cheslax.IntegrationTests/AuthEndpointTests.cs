using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cheslax.IntegrationTests;

public class AuthEndpointTests : IClassFixture<CheslaxApiFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointTests(CheslaxApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
    }

    [Fact]
    public async Task Register_login_and_me_round_trip()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"user_{suffix}",
            displayName = "Kỳ thủ Test",
            email = $"user_{suffix}@example.com",
            password = "correct-horse",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var me = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(body);
        Assert.Equal($"user_{suffix}", body.Username);
        Assert.Equal("Kỳ thủ Test", body.DisplayName);
        Assert.Equal(2, body.Ratings.Count);
        Assert.Contains(body.Ratings, rating => rating.GameId == "chess" && rating.Rating == 1000);
        Assert.Contains(body.Ratings, rating => rating.GameId == "o-an-quan" && rating.Rating == 1000);
        Assert.Empty(body.History);
        Assert.DoesNotContain("example.com", await me.Content.ReadAsStringAsync());

        await _client.PostAsync("/api/auth/logout", null);
        var anonymous = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = $"user_{suffix}",
            password = "correct-horse",
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var publicProfile = await _client.GetAsync($"/api/users/user_{suffix}");
        Assert.Equal(HttpStatusCode.OK, publicProfile.StatusCode);
        var publicJson = await publicProfile.Content.ReadAsStringAsync();
        Assert.DoesNotContain("example.com", publicJson);
    }

    [Fact]
    public async Task Login_rejects_wrong_password_with_generic_error()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"user_{suffix}",
            displayName = "Test",
            email = $"user_{suffix}@example.com",
            password = "correct-horse",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        await _client.PostAsync("/api/auth/logout", null);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = $"user_{suffix}",
            password = "wrong-password",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    private sealed record ProfileDto(
        string Username,
        string DisplayName,
        string AvatarId,
        DateTimeOffset CreatedAt,
        List<GameRatingDto> Ratings,
        List<RatingHistoryDto> History);

    private sealed record GameRatingDto(string GameId, int Rating, int HighestRating);

    private sealed record RatingHistoryDto(Guid Id, string GameId, string Result);
}
