using Cheslax.Domain;

namespace Cheslax.UnitTests;

public class EloCalculatorTests
{
    [Fact]
    public void Equal_ratings_draw_does_not_change_score()
    {
        var change = EloCalculator.Compute(1000, 1000, MatchResultKind.Draw);
        Assert.Equal(0, change.Delta);
        Assert.Equal(1000, change.NewRating);
        Assert.Equal(0.5, change.ExpectedScore, 5);
    }

    [Fact]
    public void Equal_ratings_win_gains_half_K()
    {
        var change = EloCalculator.Compute(1000, 1000, MatchResultKind.Win);
        Assert.Equal(16, change.Delta);
        Assert.Equal(1016, change.NewRating);
    }

    [Fact]
    public void Pair_updates_are_symmetric_for_a_decisive_game()
    {
        var (player, opponent) = EloCalculator.ComputePair(1200, 1000, MatchResultKind.Win);
        Assert.Equal(-player.Delta, opponent.Delta);
        Assert.True(player.NewRating > 1200);
        Assert.True(opponent.NewRating < 1000);
    }

    [Fact]
    public void Rating_never_goes_below_zero()
    {
        var change = EloCalculator.Compute(5, 5, MatchResultKind.Loss);
        Assert.Equal(0, change.NewRating);
    }

    [Fact]
    public void Fixed_ai_opponent_rating_is_deterministic_and_uses_k32()
    {
        var loss = EloCalculator.Compute(1000, 1100, MatchResultKind.Loss);
        var draw = EloCalculator.Compute(1000, 1100, MatchResultKind.Draw);
        var win = EloCalculator.Compute(1000, 1100, MatchResultKind.Win);
        Assert.Equal(loss.Delta, EloCalculator.Compute(1000, 1100, MatchResultKind.Loss).Delta);
        Assert.Equal(-12, loss.Delta);
        Assert.Equal(988, loss.NewRating);
        Assert.Equal(4, draw.Delta);
        Assert.Equal(20, win.Delta);
        Assert.True(win.Delta > draw.Delta);
        Assert.True(draw.Delta > loss.Delta);
    }
}
