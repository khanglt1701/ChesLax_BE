using Cheslax.Domain;

namespace Cheslax.UnitTests;

public class AiDifficultyPolicyTests
{
    [Theory]
    [InlineData(0, "beginner")]
    [InlineData(799, "beginner")]
    [InlineData(800, "easy")]
    [InlineData(999, "easy")]
    [InlineData(1000, "normal")]
    [InlineData(1199, "normal")]
    [InlineData(1200, "hard")]
    [InlineData(1400, "expert")]
    [InlineData(1600, "advanced")]
    [InlineData(2400, "advanced")]
    public void Maps_elo_to_configured_band(int rating, string expected)
    {
        var band = AiDifficultyPolicy.Resolve(rating, AiDifficultyPolicy.DefaultBands);
        Assert.Equal(expected, band.Id);
        Assert.True(band.OpponentRating > 0);
        Assert.NotEqual(rating, band.OpponentRating);
    }

    [Fact]
    public void Human_queue_stays_off_until_explicitly_enabled()
    {
        var options = new MatchmakingOptions
        {
            RankedMatchmakingMode = RankedMatchmakingModes.HumanOnly,
            EnableHumanRankedMatchmaking = false,
        };
        Assert.Equal(RankedMatchmakingModes.AIOnly, options.EffectiveRankedMode());
        Assert.False(options.UsesHumanQueue());
        Assert.True(options.AllowsAiOpponent());

        options.EnableHumanRankedMatchmaking = true;
        Assert.Equal(RankedMatchmakingModes.HumanOnly, options.EffectiveRankedMode());
        Assert.True(options.UsesHumanQueue());
        Assert.False(options.AllowsAiOpponent());
    }
}
