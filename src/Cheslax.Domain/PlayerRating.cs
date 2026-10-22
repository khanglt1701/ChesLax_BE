namespace Cheslax.Domain;

public sealed class PlayerRating
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public GameType GameType { get; set; }
    public int Rating { get; set; } = RankCatalog.StartingRating;
    public int PeakRating { get; set; } = RankCatalog.StartingRating;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int GamesPlayed { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public static PlayerRating CreateInitial(string userId, GameType gameType, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        GameType = gameType,
        Rating = RankCatalog.StartingRating,
        PeakRating = RankCatalog.StartingRating,
        UpdatedAt = now,
    };
}
