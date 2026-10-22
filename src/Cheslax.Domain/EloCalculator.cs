namespace Cheslax.Domain;

public enum MatchResultKind
{
    Win = 1,
    Loss = 2,
    Draw = 3,
}

public sealed record EloChange(
    int OldRating,
    int NewRating,
    int Delta,
    double ExpectedScore);

public static class EloCalculator
{
    public static double ExpectedScore(int playerRating, int opponentRating)
    {
        var player = RankCatalog.SanitizeRating(playerRating);
        var opponent = RankCatalog.SanitizeRating(opponentRating);
        return 1d / (1d + Math.Pow(10d, (opponent - player) / 400d));
    }

    public static double ActualScore(MatchResultKind result) => result switch
    {
        MatchResultKind.Win => 1d,
        MatchResultKind.Loss => 0d,
        MatchResultKind.Draw => 0.5d,
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown match result."),
    };

    public static EloChange Compute(
        int playerRating,
        int opponentRating,
        MatchResultKind result,
        int kFactor = RankCatalog.DefaultKFactor)
    {
        var oldRating = RankCatalog.SanitizeRating(playerRating);
        var expected = ExpectedScore(oldRating, opponentRating);
        var delta = (int)Math.Round(kFactor * (ActualScore(result) - expected), MidpointRounding.AwayFromZero);
        var newRating = Math.Max(0, oldRating + delta);
        return new EloChange(oldRating, newRating, delta, expected);
    }

    public static (EloChange Player, EloChange Opponent) ComputePair(
        int playerRating,
        int opponentRating,
        MatchResultKind playerResult,
        int kFactor = RankCatalog.DefaultKFactor)
    {
        var opponentResult = playerResult switch
        {
            MatchResultKind.Win => MatchResultKind.Loss,
            MatchResultKind.Loss => MatchResultKind.Win,
            MatchResultKind.Draw => MatchResultKind.Draw,
            _ => throw new ArgumentOutOfRangeException(nameof(playerResult), playerResult, "Unknown match result."),
        };

        return (
            Compute(playerRating, opponentRating, playerResult, kFactor),
            Compute(opponentRating, playerRating, opponentResult, kFactor));
    }
}
