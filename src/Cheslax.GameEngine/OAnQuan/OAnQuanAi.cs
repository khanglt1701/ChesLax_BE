namespace Cheslax.GameEngine.OAnQuan;

/// <summary>
/// Heuristic Ô ăn quan search. Not a calibrated Elo engine: it prefers captures and score
/// swings using 1–2 ply lookahead. Beginner mixes in random legal moves.
/// </summary>
public static class OAnQuanAi
{
    public static (int Pit, OAnQuanDirection Direction) ChooseMove(
        OAnQuanState state,
        int searchDepth,
        double blunderChance,
        CancellationToken cancellationToken = default)
    {
        var legal = LegalMoves(state);
        if (legal.Count == 0)
        {
            throw new InvalidOperationException("Không còn nước rải hợp lệ.");
        }

        if (blunderChance > 0 && Random.Shared.NextDouble() < blunderChance)
        {
            return legal[Random.Shared.Next(legal.Count)];
        }

        var depth = Math.Clamp(searchDepth, 0, 2);
        if (depth == 0)
        {
            return legal[Random.Shared.Next(legal.Count)];
        }

        var player = state.CurrentPlayer;
        var best = legal[0];
        var bestScore = int.MinValue;
        foreach (var move in legal)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = OAnQuanRules.ApplyMove(state, move.Pit, move.Direction).State;
            var score = depth == 1
                ? Evaluate(next, player)
                : Minimize(next, player, cancellationToken);
            if (score > bestScore)
            {
                bestScore = score;
                best = move;
            }
        }

        return best;
    }

    public static IReadOnlyList<(int Pit, OAnQuanDirection Direction)> LegalMoves(OAnQuanState state)
    {
        var pits = OAnQuanRules.GetValidPits(state);
        var moves = new List<(int Pit, OAnQuanDirection Direction)>(pits.Count * 2);
        foreach (var pit in pits)
        {
            moves.Add((pit, OAnQuanDirection.Clockwise));
            moves.Add((pit, OAnQuanDirection.CounterClockwise));
        }

        return moves;
    }

    private static int Minimize(OAnQuanState state, int rootPlayer, CancellationToken cancellationToken)
    {
        if (state.Status == "ended")
        {
            return Evaluate(state, rootPlayer);
        }

        var legal = LegalMoves(state);
        var worst = int.MaxValue;
        foreach (var move in legal)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = OAnQuanRules.ApplyMove(state, move.Pit, move.Direction).State;
            var score = Evaluate(next, rootPlayer);
            if (score < worst) worst = score;
        }

        return worst == int.MaxValue ? Evaluate(state, rootPlayer) : worst;
    }

    private static int Evaluate(OAnQuanState state, int player)
    {
        var mine = OAnQuanRules.ScoreOf(state.Captured[player]);
        var theirs = OAnQuanRules.ScoreOf(state.Captured[player == 0 ? 1 : 0]);
        var myDan = OAnQuanRules.PlayerDanPits(state, player).Sum(pit => pit.Dan);
        var theirDan = OAnQuanRules.PlayerDanPits(state, player == 0 ? 1 : 0).Sum(pit => pit.Dan);
        var terminal = 0;
        if (state.Status == "ended")
        {
            terminal = state.Winner == player.ToString() ? 400 : state.Winner == "draw" ? 0 : -400;
        }

        return (mine - theirs) * 12 + (myDan - theirDan) * 2 + terminal;
    }
}
