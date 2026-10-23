namespace Cheslax.Domain;

public enum OpponentType
{
    Human = 1,
    Ai = 2,
}

/// <summary>
/// Engine settings and a provisional opponent rating for Elo. Labels are not measured Elo.
/// SearchDepth/time/noise drive actual strength; OpponentRating is only used in the rating formula.
/// </summary>
public sealed class AiDifficultyBand
{
    public string Id { get; set; } = "normal";
    public string Label { get; set; } = "Normal";
    public int MinElo { get; set; }
    public int MaxElo { get; set; } = int.MaxValue;
    public int OpponentRating { get; set; } = 1100;
    public int SearchDepth { get; set; } = 2;
    public int Noise { get; set; }
    public double BlunderChance { get; set; }
    public int MaxNodes { get; set; } = 12_000;
    public int MaxTimeMs { get; set; } = 700;
    public bool Quiesce { get; set; }
}

public static class AiDifficultyPolicy
{
    public static IReadOnlyList<AiDifficultyBand> DefaultBands { get; } =
    [
        new()
        {
            Id = "beginner", Label = "Beginner", MinElo = 0, MaxElo = 799, OpponentRating = 700,
            SearchDepth = 1, Noise = 400, BlunderChance = 0.30, MaxNodes = 2_000, MaxTimeMs = 250,
        },
        new()
        {
            Id = "easy", Label = "Easy", MinElo = 800, MaxElo = 999, OpponentRating = 900,
            SearchDepth = 1, Noise = 220, BlunderChance = 0.18, MaxNodes = 3_000, MaxTimeMs = 350,
        },
        new()
        {
            Id = "normal", Label = "Normal", MinElo = 1000, MaxElo = 1199, OpponentRating = 1100,
            SearchDepth = 2, Noise = 70, BlunderChance = 0.05, MaxNodes = 12_000, MaxTimeMs = 700,
        },
        new()
        {
            Id = "hard", Label = "Hard", MinElo = 1200, MaxElo = 1399, OpponentRating = 1300,
            SearchDepth = 3, Noise = 0, BlunderChance = 0, MaxNodes = 28_000, MaxTimeMs = 900, Quiesce = true,
        },
        new()
        {
            Id = "expert", Label = "Expert", MinElo = 1400, MaxElo = 1599, OpponentRating = 1500,
            SearchDepth = 3, Noise = 0, BlunderChance = 0, MaxNodes = 40_000, MaxTimeMs = 1_100, Quiesce = true,
        },
        new()
        {
            Id = "advanced", Label = "Advanced", MinElo = 1600, MaxElo = int.MaxValue, OpponentRating = 1700,
            SearchDepth = 4, Noise = 0, BlunderChance = 0, MaxNodes = 55_000, MaxTimeMs = 1_400, Quiesce = true,
        },
    ];

    public static AiDifficultyBand Resolve(int playerRating, IReadOnlyList<AiDifficultyBand> bands)
    {
        var source = bands.Count == 0 ? DefaultBands : bands;
        var rating = RankCatalog.SanitizeRating(playerRating);
        return source.FirstOrDefault(band => rating >= band.MinElo && rating <= band.MaxElo)
            ?? source[^1];
    }
}
