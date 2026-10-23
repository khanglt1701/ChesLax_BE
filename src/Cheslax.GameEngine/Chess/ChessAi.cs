namespace Cheslax.GameEngine.Chess;

/// <summary>
/// Mailbox minimax with optional capture quiescence. Strength comes from depth, node/time
/// caps, noise, and blunder chance — not from the difficulty label or a measured Elo.
/// </summary>
public static class ChessAi
{
    private static readonly int[] PawnPst =
    [
        0, 0, 0, 0, 0, 0, 0, 0,
        50, 50, 50, 50, 50, 50, 50, 50,
        10, 10, 20, 30, 30, 20, 10, 10,
        5, 5, 10, 25, 25, 10, 5, 5,
        0, 0, 0, 20, 20, 0, 0, 0,
        5, -5, -10, 0, 0, -10, -5, 5,
        5, 10, 10, -20, -20, 10, 10, 5,
        0, 0, 0, 0, 0, 0, 0, 0,
    ];

    private static readonly int[] KnightPst =
    [
        -50, -40, -30, -30, -30, -30, -40, -50,
        -40, -20, 0, 0, 0, 0, -20, -40,
        -30, 0, 10, 15, 15, 10, 0, -30,
        -30, 5, 15, 20, 20, 15, 5, -30,
        -30, 0, 15, 20, 20, 15, 0, -30,
        -30, 5, 10, 15, 15, 10, 5, -30,
        -40, -20, 0, 5, 5, 0, -20, -40,
        -50, -40, -30, -30, -30, -30, -40, -50,
    ];

    public static ChessLegalMove ChooseMove(
        ChessPosition position,
        int depth,
        int noise,
        double blunderChance,
        int maxNodes,
        int maxTimeMs,
        bool quiesce,
        CancellationToken cancellationToken = default)
    {
        var legal = position.LegalMoves();
        if (legal.Count == 0)
        {
            throw new InvalidOperationException("Không còn nước đi hợp lệ.");
        }

        if (blunderChance > 0 && Random.Shared.NextDouble() < blunderChance)
        {
            return legal[Random.Shared.Next(legal.Count)];
        }

        var ctx = new SearchContext
        {
            MaxNodes = Math.Max(64, maxNodes),
            Deadline = DateTime.UtcNow.AddMilliseconds(Math.Clamp(maxTimeMs, 50, 2_000)),
            Quiesce = quiesce,
        };
        var ply = Math.Clamp(depth, 1, 4);
        ChessLegalMove best = legal[0];
        var bestScore = int.MinValue;
        foreach (var move in legal)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!position.TryMove(move.From, move.To, move.Promotion, out var applied) || applied is null)
            {
                continue;
            }

            var score = -Negamax(applied.Position, ply - 1, -30_000, 30_000, ctx, cancellationToken);
            if (noise > 0)
            {
                score += Random.Shared.Next(-noise, noise + 1);
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = move;
            }
        }

        return best;
    }

    private static int Negamax(
        ChessPosition position,
        int depth,
        int alpha,
        int beta,
        SearchContext ctx,
        CancellationToken cancellationToken)
    {
        ctx.Nodes++;
        if (cancellationToken.IsCancellationRequested || ctx.Nodes >= ctx.MaxNodes || DateTime.UtcNow >= ctx.Deadline)
        {
            return Evaluate(position);
        }

        if (position.IsCheckmate)
        {
            return -20_000 + (4 - depth);
        }

        if (position.IsStalemate || position.IsFiftyMove || position.IsThreefold || position.IsInsufficientMaterial)
        {
            return 0;
        }

        if (depth <= 0)
        {
            return ctx.Quiesce ? Quiesce(position, alpha, beta, 2, ctx, cancellationToken) : Evaluate(position);
        }

        var legal = position.LegalMoves();
        var best = int.MinValue / 4;
        foreach (var move in legal)
        {
            if (!position.TryMove(move.From, move.To, move.Promotion, out var applied) || applied is null)
            {
                continue;
            }

            var score = -Negamax(applied.Position, depth - 1, -beta, -alpha, ctx, cancellationToken);
            if (score > best) best = score;
            if (score > alpha) alpha = score;
            if (alpha >= beta) break;
        }

        return best;
    }

    private static int Quiesce(
        ChessPosition position,
        int alpha,
        int beta,
        int remain,
        SearchContext ctx,
        CancellationToken cancellationToken)
    {
        var stand = Evaluate(position);
        if (stand >= beta || remain <= 0 || ctx.Nodes >= ctx.MaxNodes)
        {
            return stand;
        }

        if (stand > alpha) alpha = stand;
        foreach (var move in position.LegalMoves())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!position.TryMove(move.From, move.To, move.Promotion, out var applied) || applied is null)
            {
                continue;
            }

            if (!IsCaptureOrPromotion(position, applied.Position, move))
            {
                continue;
            }

            var score = -Quiesce(applied.Position, -beta, -alpha, remain - 1, ctx, cancellationToken);
            if (score >= beta) return score;
            if (score > alpha) alpha = score;
        }

        return alpha;
    }

    private static bool IsCaptureOrPromotion(ChessPosition before, ChessPosition after, ChessLegalMove move)
    {
        if (move.Promotion is not null) return true;
        var from = SquareIndex(move.From);
        var to = SquareIndex(move.To);
        return before.PieceAt(to) != '\0' || (char.ToLowerInvariant(before.PieceAt(from)) == 'p' && after.PieceAt(to) != before.PieceAt(from) && before.PieceAt(to) == '\0' && after.PieceAt(from) == '\0');
    }

    private static int Evaluate(ChessPosition position)
    {
        var score = 0;
        for (var i = 0; i < 64; i++)
        {
            var material = position.MaterialOn(i);
            if (material == 0) continue;
            score += material;
            var piece = char.ToLowerInvariant(position.PieceAt(i));
            var white = material > 0;
            var pstIndex = white ? 56 - (i / 8) * 8 + i % 8 : i;
            var pst = piece switch
            {
                'p' => PawnPst[pstIndex],
                'n' => KnightPst[pstIndex],
                _ => 0,
            };
            score += white ? pst : -pst;
        }

        return position.Turn == 'w' ? score : -score;
    }

    private static int SquareIndex(string name) => (name[1] - '1') * 8 + (name[0] - 'a');

    private sealed class SearchContext
    {
        public int Nodes { get; set; }
        public int MaxNodes { get; set; }
        public DateTime Deadline { get; set; }
        public bool Quiesce { get; set; }
    }
}
