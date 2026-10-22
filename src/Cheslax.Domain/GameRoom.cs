namespace Cheslax.Domain;

public sealed class GameRoom
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public GameType GameType { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Waiting;
    public string TimeControlId { get; set; } = "off";
    public bool Rated { get; set; } = true;
    public string HostUserId { get; set; } = string.Empty;
    public Guid? CurrentMatchId { get; set; }
    public int Version { get; set; }
    public string? StateJson { get; set; }
    public int? Player1ClockMs { get; set; }
    public int? Player2ClockMs { get; set; }
    public string? ClockRunningSide { get; set; }
    public DateTimeOffset? ClockLastStartedAt { get; set; }
    public string? DrawOfferedByUserId { get; set; }
    public DateTimeOffset? DrawOfferedAt { get; set; }
    public DateTimeOffset? DisconnectDeadline { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<GameRoomSeat> Seats { get; set; } = [];
    public ICollection<GameMatch> Matches { get; set; } = [];
}
