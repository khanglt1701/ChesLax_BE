namespace Cheslax.GameEngine.Chess;

public sealed record ChessMoveResult(ChessPosition Position, string San, string From, string To, char? Promotion);

public sealed class ChessPosition
{
    public const string StartingFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    internal char[] Squares { get; }
    public char Turn { get; private set; } = 'w';
    public string Castling { get; private set; } = "KQkq";
    public string EnPassant { get; private set; } = "";
    public int Halfmove { get; private set; }
    public int Fullmove { get; private set; }
    public List<string> History { get; }
    public List<string> PositionKeys { get; }

    public ChessPosition()
        : this(StartingFen, [], [])
    {
    }

    public ChessPosition(string fen)
        : this(fen, [], [])
    {
    }

    private ChessPosition(string fen, List<string> history, List<string> keys)
    {
        Squares = new char[64];
        History = [.. history];
        PositionKeys = [.. keys];
        ParseFen(fen);
        if (PositionKeys.Count == 0)
        {
            PositionKeys.Add(PositionKey());
        }
    }

    public string Fen
    {
        get
        {
            var parts = new List<string>();
            for (var rank = 7; rank >= 0; rank--)
            {
                var empty = 0;
                var row = "";
                for (var file = 0; file < 8; file++)
                {
                    var piece = Squares[Index(file, rank)];
                    if (piece == '\0')
                    {
                        empty++;
                        continue;
                    }

                    if (empty > 0)
                    {
                        row += empty;
                        empty = 0;
                    }

                    row += piece;
                }

                if (empty > 0) row += empty;
                parts.Add(row);
            }

            return $"{string.Join('/', parts)} {Turn} {(string.IsNullOrEmpty(Castling) ? "-" : Castling)} {(string.IsNullOrEmpty(EnPassant) ? "-" : EnPassant)} {Halfmove} {Fullmove}";
        }
    }

    public bool InCheck => IsKingAttacked(Turn);
    public bool IsCheckmate => InCheck && !HasLegalMove();
    public bool IsStalemate => !InCheck && !HasLegalMove();
    public bool IsFiftyMove => Halfmove >= 100;
    public bool IsThreefold => PositionKeys.Count(key => key == PositionKey()) >= 3;
    public bool IsInsufficientMaterial => InsufficientMaterial();
    public bool IsGameOver => IsCheckmate || IsStalemate || IsFiftyMove || IsThreefold || IsInsufficientMaterial;

    public string Status =>
        IsCheckmate ? "checkmate" :
        IsStalemate ? "stalemate" :
        IsInsufficientMaterial ? "draw-insufficient" :
        IsThreefold ? "draw-threefold" :
        IsFiftyMove ? "draw-fifty" :
        InCheck ? "check" :
        "playing";

    public ChessPosition Clone() => new(Fen, History, PositionKeys);

    public bool TryMove(string from, string to, char? promotion, out ChessMoveResult? result)
    {
        result = null;
        if (!TryParseSquare(from, out var fromIndex) || !TryParseSquare(to, out var toIndex))
        {
            return false;
        }

        var legal = GenerateLegalMoves().FirstOrDefault(move => move.From == fromIndex && move.To == toIndex && move.Promotion == NormalizePromotion(promotion, Squares[fromIndex]));
        if (legal is null)
        {
            if (promotion is null)
            {
                var promotions = GenerateLegalMoves().Where(move => move.From == fromIndex && move.To == toIndex && move.Promotion is not null).ToList();
                if (promotions.Count == 1)
                {
                    legal = promotions[0];
                }
            }
        }

        if (legal is null)
        {
            return false;
        }

        var next = Clone();
        next.ApplyInternal(legal);
        result = new ChessMoveResult(next, legal.San, SquareName(legal.From), SquareName(legal.To), legal.Promotion);
        return true;
    }

    private void ApplyInternal(GenMove move)
    {
        var piece = Squares[move.From];
        var captured = Squares[move.To];
        if (move.EnPassant)
        {
            var capIndex = Index(File(move.To), Rank(move.From));
            captured = Squares[capIndex];
            Squares[capIndex] = '\0';
        }

        Squares[move.To] = move.Promotion ?? piece;
        Squares[move.From] = '\0';
        if (move.CastleKing)
        {
            Squares[Index(5, Rank(move.From))] = Squares[Index(7, Rank(move.From))];
            Squares[Index(7, Rank(move.From))] = '\0';
        }
        else if (move.CastleQueen)
        {
            Squares[Index(3, Rank(move.From))] = Squares[Index(0, Rank(move.From))];
            Squares[Index(0, Rank(move.From))] = '\0';
        }

        Castling = UpdateCastling(Castling, move.From, move.To);
        EnPassant = move.DoublePawn ? SquareName(Index(File(move.From), Rank(move.From) + (Turn == 'w' ? 1 : -1))) : "";
        var isPawn = char.ToLowerInvariant(piece) == 'p';
        Halfmove = isPawn || captured != '\0' ? 0 : Halfmove + 1;
        if (Turn == 'b') Fullmove++;
        Turn = Turn == 'w' ? 'b' : 'w';
        History.Add(move.San);
        PositionKeys.Add(PositionKey());
    }

    private List<GenMove> GenerateLegalMoves()
    {
        var pseudo = GeneratePseudoMoves(Turn);
        var legal = new List<GenMove>();
        foreach (var move in pseudo)
        {
            var probe = Clone();
            probe.ApplyInternal(new GenMove(move.From, move.To, move.Promotion, move.EnPassant, move.DoublePawn, move.CastleKing, move.CastleQueen, ""));
            if (!probe.IsKingAttacked(Turn))
            {
                move.San = ToSan(move, pseudo);
                legal.Add(move);
            }
        }

        return legal;
    }

    private bool HasLegalMove() => GenerateLegalMoves().Count > 0;

    private List<GenMove> GeneratePseudoMoves(char side)
    {
        var moves = new List<GenMove>();
        var white = side == 'w';
        for (var i = 0; i < 64; i++)
        {
            var piece = Squares[i];
            if (piece == '\0' || char.IsUpper(piece) != white) continue;
            switch (char.ToLowerInvariant(piece))
            {
                case 'p': AddPawnMoves(moves, i, white); break;
                case 'n': AddSlideMoves(moves, i, white, [(-1, 2), (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1)], 1); break;
                case 'b': AddSlideMoves(moves, i, white, [(-1, 1), (1, 1), (1, -1), (-1, -1)], 8); break;
                case 'r': AddSlideMoves(moves, i, white, [(-1, 0), (1, 0), (0, 1), (0, -1)], 8); break;
                case 'q': AddSlideMoves(moves, i, white, [(-1, 0), (1, 0), (0, 1), (0, -1), (-1, 1), (1, 1), (1, -1), (-1, -1)], 8); break;
                case 'k': AddKingMoves(moves, i, white); break;
            }
        }

        return moves;
    }

    private void AddPawnMoves(List<GenMove> moves, int from, bool white)
    {
        var dir = white ? 1 : -1;
        var startRank = white ? 1 : 6;
        var promoRank = white ? 7 : 0;
        var file = File(from);
        var rank = Rank(from);
            var one = Index(file, rank + dir);
        if (InBoard(file, rank + dir) && Squares[one] == '\0')
        {
            AddPawnTo(moves, from, one, promoRank, white, false, false);
            var two = Index(file, rank + dir * 2);
            if (rank == startRank && Squares[two] == '\0')
            {
                moves.Add(new GenMove(from, two, null, false, true, false, false, ""));
            }
        }

        foreach (var df in new[] { -1, 1 })
        {
            if (!InBoard(file + df, rank + dir)) continue;
            var to = Index(file + df, rank + dir);
            if (Squares[to] != '\0' && char.IsUpper(Squares[to]) != white)
            {
                AddPawnTo(moves, from, to, promoRank, white, false, false);
            }
            else if (!string.IsNullOrEmpty(EnPassant) && SquareName(to) == EnPassant)
            {
                moves.Add(new GenMove(from, to, null, true, false, false, false, ""));
            }
        }
    }

    private static void AddPawnTo(List<GenMove> moves, int from, int to, int promoRank, bool white, bool ep, bool dbl)
    {
        if (Rank(to) == promoRank)
        {
            foreach (var promo in white ? new[] { 'Q', 'R', 'B', 'N' } : new[] { 'q', 'r', 'b', 'n' })
            {
                moves.Add(new GenMove(from, to, promo, ep, dbl, false, false, ""));
            }
        }
        else
        {
            moves.Add(new GenMove(from, to, null, ep, dbl, false, false, ""));
        }
    }

    private void AddSlideMoves(List<GenMove> moves, int from, bool white, (int df, int dr)[] deltas, int max)
    {
        foreach (var (df, dr) in deltas)
        {
            for (var step = 1; step <= max; step++)
            {
                var file = File(from) + df * step;
                var rank = Rank(from) + dr * step;
                if (!InBoard(file, rank)) break;
                var to = Index(file, rank);
                var occupant = Squares[to];
                if (occupant == '\0')
                {
                    moves.Add(new GenMove(from, to, null, false, false, false, false, ""));
                    continue;
                }

                if (char.IsUpper(occupant) != white)
                {
                    moves.Add(new GenMove(from, to, null, false, false, false, false, ""));
                }

                break;
            }
        }
    }

    private void AddKingMoves(List<GenMove> moves, int from, bool white)
    {
        AddSlideMoves(moves, from, white, [(-1, 0), (1, 0), (0, 1), (0, -1), (-1, 1), (1, 1), (1, -1), (-1, -1)], 1);
        var rank = white ? 0 : 7;
        if (from != Index(4, rank)) return;
        var enemy = white ? 'b' : 'w';
        if (Castling.Contains(white ? 'K' : 'k') && Squares[Index(5, rank)] == '\0' && Squares[Index(6, rank)] == '\0'
            && !IsSquareAttacked(Index(4, rank), enemy) && !IsSquareAttacked(Index(5, rank), enemy) && !IsSquareAttacked(Index(6, rank), enemy))
        {
            moves.Add(new GenMove(from, Index(6, rank), null, false, false, true, false, ""));
        }

        if (Castling.Contains(white ? 'Q' : 'q') && Squares[Index(3, rank)] == '\0' && Squares[Index(2, rank)] == '\0' && Squares[Index(1, rank)] == '\0'
            && !IsSquareAttacked(Index(4, rank), enemy) && !IsSquareAttacked(Index(3, rank), enemy) && !IsSquareAttacked(Index(2, rank), enemy))
        {
            moves.Add(new GenMove(from, Index(2, rank), null, false, false, false, true, ""));
        }
    }

    private bool IsKingAttacked(char side)
    {
        var king = side == 'w' ? 'K' : 'k';
        var index = Array.IndexOf(Squares, king);
        return index >= 0 && IsSquareAttacked(index, side == 'w' ? 'b' : 'w');
    }

    private bool IsSquareAttacked(int target, char bySide)
    {
        var white = bySide == 'w';
        var pawnDir = white ? -1 : 1;
        var tf = File(target);
        var tr = Rank(target);
        foreach (var df in new[] { -1, 1 })
        {
            if (InBoard(tf + df, tr + pawnDir) && Squares[Index(tf + df, tr + pawnDir)] == (white ? 'P' : 'p'))
            {
                return true;
            }
        }

        foreach (var (df, dr) in new[] { (-1, 2), (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1) })
        {
            if (InBoard(tf + df, tr + dr) && Squares[Index(tf + df, tr + dr)] == (white ? 'N' : 'n')) return true;
        }

        if (AttacksSlide(target, white, [(-1, 1), (1, 1), (1, -1), (-1, -1)], white ? "BQ" : "bq")) return true;
        if (AttacksSlide(target, white, [(-1, 0), (1, 0), (0, 1), (0, -1)], white ? "RQ" : "rq")) return true;
        foreach (var (df, dr) in new[] { (-1, 0), (1, 0), (0, 1), (0, -1), (-1, 1), (1, 1), (1, -1), (-1, -1) })
        {
            if (InBoard(tf + df, tr + dr) && Squares[Index(tf + df, tr + dr)] == (white ? 'K' : 'k')) return true;
        }

        return false;
    }

    private bool AttacksSlide(int target, bool white, (int df, int dr)[] deltas, string attackers)
    {
        var tf = File(target);
        var tr = Rank(target);
        foreach (var (df, dr) in deltas)
        {
            for (var step = 1; step <= 8; step++)
            {
                var file = tf + df * step;
                var rank = tr + dr * step;
                if (!InBoard(file, rank)) break;
                var piece = Squares[Index(file, rank)];
                if (piece == '\0') continue;
                if (attackers.Contains(piece)) return true;
                break;
            }
        }

        return false;
    }

    private bool InsufficientMaterial()
    {
        var pieces = Squares.Where(piece => piece != '\0').ToList();
        if (pieces.Count <= 2) return true;
        var others = pieces.Where(piece => char.ToLowerInvariant(piece) != 'k').ToList();
        if (others.Count == 1 && others.All(piece => "nbNB".Contains(piece))) return true;
        if (others.Count == 2 && others.All(piece => char.ToLowerInvariant(piece) == 'b'))
        {
            var bishops = Enumerable.Range(0, 64).Where(i => char.ToLowerInvariant(Squares[i]) == 'b').ToList();
            if (bishops.Count == 2)
            {
                return Color(bishops[0]) == Color(bishops[1]);
            }
        }

        return false;
    }

    private string ToSan(GenMove move, List<GenMove> pseudo)
    {
        if (move.CastleKing) return "O-O";
        if (move.CastleQueen) return "O-O-O";
        var piece = Squares[move.From];
        var kind = char.ToUpperInvariant(piece);
        var dest = SquareName(move.To);
        var capture = Squares[move.To] != '\0' || move.EnPassant;
        var promo = move.Promotion is null ? "" : $"={char.ToUpperInvariant(move.Promotion.Value)}";
        if (kind == 'P')
        {
            var text = capture ? $"{SquareName(move.From)[0]}x{dest}{promo}" : $"{dest}{promo}";
            return text;
        }

        var same = pseudo.Where(item => item != move && Squares[item.From] == piece && item.To == move.To && item.Promotion == move.Promotion).ToList();
        var fromName = SquareName(move.From);
        var disambig = "";
        if (same.Count > 0)
        {
            var fileClash = same.Any(item => File(item.From) == File(move.From));
            var rankClash = same.Any(item => Rank(item.From) == Rank(move.From));
            if (!fileClash) disambig = fromName[0].ToString();
            else if (!rankClash) disambig = fromName[1].ToString();
            else disambig = fromName;
        }

        return $"{kind}{disambig}{(capture ? "x" : "")}{dest}{promo}";
    }

    private string PositionKey() => string.Concat(Fen.Split(' ').Take(4));

    private void ParseFen(string fen)
    {
        var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) throw new ArgumentException("Invalid FEN.", nameof(fen));
        var ranks = parts[0].Split('/');
        Array.Fill(Squares, '\0');
        for (var rank = 0; rank < 8; rank++)
        {
            var file = 0;
            foreach (var symbol in ranks[7 - rank])
            {
                if (char.IsDigit(symbol))
                {
                    file += symbol - '0';
                    continue;
                }

                Squares[Index(file, rank)] = symbol;
                file++;
            }
        }

        Turn = parts[1][0];
        Castling = parts[2] == "-" ? "" : parts[2];
        EnPassant = parts[3] == "-" ? "" : parts[3];
        Halfmove = parts.Length > 4 && int.TryParse(parts[4], out var half) ? half : 0;
        Fullmove = parts.Length > 5 && int.TryParse(parts[5], out var full) ? full : 1;
    }

    private static char? NormalizePromotion(char? promotion, char piece)
    {
        if (promotion is null) return null;
        var white = char.IsUpper(piece);
        var letter = char.ToLowerInvariant(promotion.Value);
        if (letter is not ('q' or 'r' or 'b' or 'n')) return white ? 'Q' : 'q';
        return white ? char.ToUpperInvariant(letter) : letter;
    }

    private static string UpdateCastling(string rights, int from, int to)
    {
        var next = rights;
        void Drop(char flag) => next = next.Replace(flag.ToString(), "", StringComparison.Ordinal);
        if (from == Index(4, 0) || to == Index(4, 0)) { Drop('K'); Drop('Q'); }
        if (from == Index(4, 7) || to == Index(4, 7)) { Drop('k'); Drop('q'); }
        if (from == Index(0, 0) || to == Index(0, 0)) Drop('Q');
        if (from == Index(7, 0) || to == Index(7, 0)) Drop('K');
        if (from == Index(0, 7) || to == Index(0, 7)) Drop('q');
        if (from == Index(7, 7) || to == Index(7, 7)) Drop('k');
        return next;
    }

    private static int Index(int file, int rank) => rank * 8 + file;
    private static int File(int index) => index % 8;
    private static int Rank(int index) => index / 8;
    private static int Color(int index) => (File(index) + Rank(index)) % 2;
    private static bool InBoard(int file, int rank) => file is >= 0 and < 8 && rank is >= 0 and < 8;
    private static string SquareName(int index) => $"{(char)('a' + File(index))}{Rank(index) + 1}";
    private static bool TryParseSquare(string name, out int index)
    {
        index = 0;
        if (name.Length != 2) return false;
        var file = name[0] - 'a';
        var rank = name[1] - '1';
        if (!InBoard(file, rank)) return false;
        index = Index(file, rank);
        return true;
    }

    private sealed class GenMove(int from, int to, char? promotion, bool enPassant, bool doublePawn, bool castleKing, bool castleQueen, string san)
    {
        public int From { get; } = from;
        public int To { get; } = to;
        public char? Promotion { get; } = promotion;
        public bool EnPassant { get; } = enPassant;
        public bool DoublePawn { get; } = doublePawn;
        public bool CastleKing { get; } = castleKing;
        public bool CastleQueen { get; } = castleQueen;
        public string San { get; set; } = san;
    }
}
