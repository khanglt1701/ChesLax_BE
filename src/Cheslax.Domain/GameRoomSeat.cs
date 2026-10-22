namespace Cheslax.Domain;

public sealed class GameRoomSeat
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string Side { get; set; } = string.Empty;
    public bool Connected { get; set; } = true;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LastDisconnectedAt { get; set; }

    public GameRoom? Room { get; set; }
}
