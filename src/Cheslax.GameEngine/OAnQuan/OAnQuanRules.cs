using Cheslax.Domain;

namespace Cheslax.GameEngine.OAnQuan;

public static class OAnQuanRules
{
    public static OAnQuanState CreateInitialState()
    {
        var pits = new List<Pit>();
        for (var i = 0; i < 5; i++)
        {
            pits.Add(CreatePit(i, isQuan: false, OAnQuanState.DanPerPit, 0, owner: 0));
        }

        pits.Add(CreatePit(OAnQuanState.RightQuanIndex, isQuan: true, 0, 1, owner: null));
        for (var i = 6; i < 11; i++)
        {
            pits.Add(CreatePit(i, isQuan: false, OAnQuanState.DanPerPit, 0, owner: 1));
        }

        pits.Add(CreatePit(OAnQuanState.LeftQuanIndex, isQuan: true, 0, 1, owner: null));

        return new OAnQuanState { Pits = pits, CurrentPlayer = 0 };
    }

    public static int NextIndex(int index, OAnQuanDirection direction) =>
        direction == OAnQuanDirection.Clockwise
            ? (index + 1) % OAnQuanState.PitCount
            : (index + OAnQuanState.PitCount - 1) % OAnQuanState.PitCount;

    public static int ScoreOf(CapturedPieces captured) =>
        captured.Dan + captured.Quan * OAnQuanState.QuanValue;

    public static bool BothQuanPitsEmpty(OAnQuanState state) =>
        state.Pits[OAnQuanState.LeftQuanIndex].IsEmpty && state.Pits[OAnQuanState.RightQuanIndex].IsEmpty;

    public static IReadOnlyList<Pit> PlayerDanPits(OAnQuanState state, int player) =>
        state.Pits.Where(pit => pit.Owner == player).ToList();

    public static bool PlayerHasMove(OAnQuanState state, int player) =>
        PlayerDanPits(state, player).Any(pit => pit.Dan > 0);

    public static IReadOnlyList<int> GetValidPits(OAnQuanState state, int? player = null)
    {
        if (state.Status != "playing")
        {
            return [];
        }

        var current = player ?? state.CurrentPlayer;
        return PlayerDanPits(state, current).Where(pit => pit.Dan > 0).Select(pit => pit.Index).ToList();
    }

    public static bool IsValidMove(OAnQuanState state, int pitIndex, int? player = null) =>
        GetValidPits(state, player).Contains(pitIndex);

    public static (OAnQuanState State, List<OAnQuanEvent> Events) ApplyResupplyIfNeeded(OAnQuanState state)
    {
        var next = state.Clone();
        var events = new List<OAnQuanEvent>();
        next.Resupplied = false;
        if (next.Status != "playing" || PlayerHasMove(next, next.CurrentPlayer))
        {
            return (next, events);
        }

        var captured = next.Captured[next.CurrentPlayer];
        if (ScoreOf(captured) < OAnQuanState.ResupplyCost)
        {
            DecideWinner(next, OAnQuanEndReason.CannotResupply);
            events.Add(new GameEndEvent(next.Winner ?? "draw", OAnQuanEndReason.CannotResupply));
            return (next, events);
        }

        var need = OAnQuanState.ResupplyCost;
        var convertedQuan = 0;
        var takeDan = Math.Min(captured.Dan, need);
        captured.Dan -= takeDan;
        need -= takeDan;
        while (need > 0 && captured.Quan > 0)
        {
            captured.Quan -= 1;
            convertedQuan += 1;
            var converted = OAnQuanState.QuanValue;
            var used = Math.Min(converted, need);
            need -= used;
            captured.Dan += converted - used;
        }

        foreach (var pit in PlayerDanPits(next, next.CurrentPlayer))
        {
            pit.Dan += 1;
        }

        next.Resupplied = true;
        events.Add(new ResupplyEvent(next.CurrentPlayer, convertedQuan));
        return (next, events);
    }

    public static OAnQuanMoveResult ApplyMove(OAnQuanState state, int pitIndex, OAnQuanDirection direction)
    {
        if (state.Status != "playing")
        {
            throw new InvalidOperationException("Ván đã kết thúc.");
        }

        if (!IsValidMove(state, pitIndex))
        {
            throw new InvalidOperationException("Ô không hợp lệ hoặc không thuộc lượt hiện tại.");
        }

        var next = state.Clone();
        next.Resupplied = false;
        var events = new List<OAnQuanEvent>();
        var pit = next.Pits[pitIndex];
        var hand = pit.Dan;
        pit.Dan = 0;
        events.Add(new PickEvent(pitIndex, hand));

        var current = pitIndex;
        var capturedDan = 0;
        var capturedQuan = 0;

        while (hand > 0)
        {
            while (hand > 0)
            {
                current = NextIndex(current, direction);
                next.Pits[current].Dan += 1;
                hand -= 1;
                events.Add(new DropEvent(current));
            }

            var n1 = NextIndex(current, direction);
            var n2 = NextIndex(n1, direction);
            var nextPit = next.Pits[n1];

            if (!nextPit.IsQuan && nextPit.Dan > 0)
            {
                hand = nextPit.Dan;
                nextPit.Dan = 0;
                current = n1;
                events.Add(new RelayEvent(n1, hand));
                continue;
            }

            if (nextPit.IsQuan && !nextPit.IsEmpty)
            {
                break;
            }

            if (nextPit.IsEmpty && !next.Pits[n2].IsEmpty)
            {
                var emptyIndex = n1;
                var targetIndex = n2;
                while (next.Pits[emptyIndex].IsEmpty && !next.Pits[targetIndex].IsEmpty)
                {
                    capturedDan += next.Pits[targetIndex].Dan;
                    capturedQuan += next.Pits[targetIndex].Quan;
                    CapturePit(next, targetIndex, events);
                    emptyIndex = NextIndex(targetIndex, direction);
                    targetIndex = NextIndex(emptyIndex, direction);
                }
            }

            break;
        }

        if (BothQuanPitsEmpty(next))
        {
            CollectRemaining(next);
            DecideWinner(next, OAnQuanEndReason.BothQuanEmpty);
            events.Add(new GameEndEvent(next.Winner ?? "draw", OAnQuanEndReason.BothQuanEmpty));
        }
        else
        {
            next.CurrentPlayer = next.CurrentPlayer == 0 ? 1 : 0;
            events.Add(new TurnEndEvent(next.CurrentPlayer));
            var resupply = ApplyResupplyIfNeeded(next);
            next = resupply.State;
            events.AddRange(resupply.Events);
        }

        var description = capturedDan > 0 || capturedQuan > 0
            ? $"Người chơi {state.CurrentPlayer + 1} rải từ ô {pitIndex}, ăn {capturedDan} dân{(capturedQuan > 0 ? $" và {capturedQuan} quan" : "")}"
            : $"Người chơi {state.CurrentPlayer + 1} rải từ ô {pitIndex}";

        return new OAnQuanMoveResult(next, events, description);
    }

    public static OAnQuanMoveResult ApplyMoveForPlayer(OAnQuanState state, int pitIndex, OAnQuanDirection direction, int playerId)
    {
        if (state.CurrentPlayer != playerId)
        {
            throw new InvalidOperationException("Không phải lượt của bạn.");
        }

        return ApplyMove(state, pitIndex, direction);
    }

    public static OAnQuanState Resign(OAnQuanState state, int playerId)
    {
        var next = state.Clone();
        next.Status = "ended";
        next.EndReason = OAnQuanEndReason.Resignation;
        next.Winner = playerId == 0 ? "1" : "0";
        return next;
    }

    private static Pit CreatePit(int index, bool isQuan, int dan, int quan, int? owner) => new()
    {
        Index = index,
        IsQuan = isQuan,
        Dan = dan,
        Quan = quan,
        Owner = owner,
    };

    private static void CapturePit(OAnQuanState state, int pitIndex, List<OAnQuanEvent> events)
    {
        var pit = state.Pits[pitIndex];
        var dan = pit.Dan;
        var quan = pit.Quan;
        if (dan == 0 && quan == 0)
        {
            return;
        }

        state.Captured[state.CurrentPlayer].Dan += dan;
        state.Captured[state.CurrentPlayer].Quan += quan;
        pit.Dan = 0;
        pit.Quan = 0;
        events.Add(new CaptureEvent(pitIndex, dan, quan));
    }

    private static void CollectRemaining(OAnQuanState state)
    {
        foreach (var pit in state.Pits)
        {
            if (pit.Owner is null || pit.Dan == 0)
            {
                continue;
            }

            state.Captured[pit.Owner.Value].Dan += pit.Dan;
            pit.Dan = 0;
        }
    }

    private static void DecideWinner(OAnQuanState state, OAnQuanEndReason reason)
    {
        state.Status = "ended";
        state.EndReason = reason;
        if (reason == OAnQuanEndReason.CannotResupply)
        {
            state.Winner = state.CurrentPlayer == 0 ? "1" : "0";
            return;
        }

        var s0 = ScoreOf(state.Captured[0]);
        var s1 = ScoreOf(state.Captured[1]);
        state.Winner = s0 > s1 ? "0" : s1 > s0 ? "1" : "draw";
    }
}

public sealed class OAnQuanEngine : IGameEngine
{
    public GameType GameType => GameType.OAnQuan;

    public OAnQuanState CreateMatch() => OAnQuanRules.CreateInitialState();

    public OAnQuanMoveResult Move(OAnQuanState state, int pit, OAnQuanDirection direction, int playerId) =>
        OAnQuanRules.ApplyMoveForPlayer(state, pit, direction, playerId);
}
