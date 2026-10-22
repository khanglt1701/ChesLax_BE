namespace Cheslax.GameEngine.OAnQuan;

public enum OAnQuanDirection
{
    Clockwise,
    CounterClockwise,
}

public enum OAnQuanEndReason
{
    BothQuanEmpty,
    CannotResupply,
    Resignation,
}

public sealed class Pit
{
    public int Index { get; set; }
    public bool IsQuan { get; set; }
    public int Dan { get; set; }
    public int Quan { get; set; }
    public int? Owner { get; set; }

    public bool IsEmpty => Dan == 0 && Quan == 0;

    public Pit Clone() => new()
    {
        Index = Index,
        IsQuan = IsQuan,
        Dan = Dan,
        Quan = Quan,
        Owner = Owner,
    };
}

public sealed class CapturedPieces
{
    public int Dan { get; set; }
    public int Quan { get; set; }

    public CapturedPieces Clone() => new() { Dan = Dan, Quan = Quan };
}

public sealed class OAnQuanState
{
    public const int PitCount = 12;
    public const int DanPerPit = 5;
    public const int QuanValue = 5;
    public const int ResupplyCost = 5;
    public const int RightQuanIndex = 5;
    public const int LeftQuanIndex = 11;

    public List<Pit> Pits { get; set; } = [];
    public int CurrentPlayer { get; set; }
    public CapturedPieces[] Captured { get; set; } = [new(), new()];
    public string Status { get; set; } = "playing";
    public string? Winner { get; set; }
    public OAnQuanEndReason? EndReason { get; set; }
    public bool Resupplied { get; set; }

    public OAnQuanState Clone() => new()
    {
        Pits = Pits.Select(pit => pit.Clone()).ToList(),
        CurrentPlayer = CurrentPlayer,
        Captured = [Captured[0].Clone(), Captured[1].Clone()],
        Status = Status,
        Winner = Winner,
        EndReason = EndReason,
        Resupplied = Resupplied,
    };
}

public abstract record OAnQuanEvent;

public sealed record PickEvent(int Pit, int Dan) : OAnQuanEvent;
public sealed record DropEvent(int Pit) : OAnQuanEvent;
public sealed record RelayEvent(int Pit, int Dan) : OAnQuanEvent;
public sealed record CaptureEvent(int Pit, int Dan, int Quan) : OAnQuanEvent;
public sealed record ResupplyEvent(int Player, int ConvertedQuan) : OAnQuanEvent;
public sealed record TurnEndEvent(int NextPlayer) : OAnQuanEvent;
public sealed record GameEndEvent(string Winner, OAnQuanEndReason Reason) : OAnQuanEvent;

public sealed record OAnQuanMoveResult(OAnQuanState State, IReadOnlyList<OAnQuanEvent> Events, string Description);
