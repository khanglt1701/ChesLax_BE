namespace Cheslax.Domain;

public sealed class RatingHistory
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid MatchId { get; set; }
    public GameType GameType { get; set; }
    public MatchResultKind Result { get; set; }
    public int OldRating { get; set; }
    public int NewRating { get; set; }
    public int Delta { get; set; }
    public string OldRankId { get; set; } = string.Empty;
    public string NewRankId { get; set; } = string.Empty;
    public string? OpponentUserId { get; set; }
    public string OpponentName { get; set; } = string.Empty;
    public int OpponentRating { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
