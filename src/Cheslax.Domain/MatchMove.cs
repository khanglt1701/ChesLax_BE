namespace Cheslax.Domain;

public sealed class MatchMove
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public int Sequence { get; set; }
    public string PlayerUserId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int? ClockMsRemaining { get; set; }
    public DateTimeOffset PlayedAt { get; set; }

    public GameMatch? Match { get; set; }
}
