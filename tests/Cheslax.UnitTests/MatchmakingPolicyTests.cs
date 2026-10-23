using Cheslax.Domain;

namespace Cheslax.UnitTests;

public class MatchmakingPolicyTests
{
    private static readonly MatchmakingOptions Options = new();

    [Theory]
    [InlineData(0, 100)]
    [InlineData(9, 100)]
    [InlineData(10, 200)]
    [InlineData(19, 200)]
    [InlineData(20, 300)]
    [InlineData(30, 300)]
    [InlineData(40, 350)]
    [InlineData(50, 400)]
    public void Spread_expands_with_queue_duration(int seconds, int expected)
    {
        Assert.Equal(expected, MatchmakingPolicy.AllowedSpread(TimeSpan.FromSeconds(seconds), Options));
    }

    [Fact]
    public void Finds_closest_opponent_inside_range()
    {
        var seeker = Ticket("a", 1000);
        var close = Ticket("b", 1040);
        var far = Ticket("c", 1300);
        var found = MatchmakingPolicy.FindClosestOpponent(seeker, [seeker, close, far], 100);
        Assert.Equal("b", found?.UserId);
    }

    [Fact]
    public void Does_not_match_outside_range_or_self()
    {
        var seeker = Ticket("a", 1000);
        var far = Ticket("b", 1400);
        Assert.Null(MatchmakingPolicy.FindClosestOpponent(seeker, [seeker, far], 100));
        Assert.Null(MatchmakingPolicy.FindClosestOpponent(seeker, [seeker], 800));
    }

    [Fact]
    public void Does_not_match_a_busy_player()
    {
        var seeker = Ticket("a", 1000);
        var busy = Ticket("b", 1000);
        Assert.Null(MatchmakingPolicy.FindClosestOpponent(seeker, [seeker, busy], 100, new HashSet<string> { "b" }));
    }

    private static MatchmakingTicket Ticket(string userId, int rating) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        GameType = GameType.Chess,
        RatingAtEnqueue = rating,
        EnqueuedAt = DateTimeOffset.UtcNow,
        Status = MatchmakingTicketStatus.Queued,
    };
}
