namespace Cheslax.Domain;

public sealed class MatchmakingTicket
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public GameType GameType { get; set; }
    public int RatingAtEnqueue { get; set; }
    public DateTimeOffset EnqueuedAt { get; set; }
    public MatchmakingTicketStatus Status { get; set; } = MatchmakingTicketStatus.Queued;
    public Guid? MatchId { get; set; }
    public string? ConnectionId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class ActiveMatchLock
{
    public string UserId { get; set; } = string.Empty;
    public Guid MatchId { get; set; }
    public Guid RoomId { get; set; }
    public GameType GameType { get; set; }
    public MatchMode Mode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
