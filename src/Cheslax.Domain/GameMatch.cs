namespace Cheslax.Domain;

public sealed class GameMatch
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public GameType GameType { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Waiting;
    public bool Rated { get; set; } = true;
    public string TimeControlId { get; set; } = "off";
    public string Player1UserId { get; set; } = string.Empty;
    public string Player2UserId { get; set; } = string.Empty;
    public string Player1Side { get; set; } = string.Empty;
    public string Player2Side { get; set; } = string.Empty;
    public int Player1RatingBefore { get; set; }
    public int Player2RatingBefore { get; set; }
    public int? Player1RatingAfter { get; set; }
    public int? Player2RatingAfter { get; set; }
    public MatchOutcome? Outcome { get; set; }
    public ResultReason? ResultReason { get; set; }
    public string? StateSnapshot { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public GameRoom? Room { get; set; }
    public ICollection<MatchMove> Moves { get; set; } = [];
    public ICollection<RatingHistory> RatingChanges { get; set; } = [];
}
