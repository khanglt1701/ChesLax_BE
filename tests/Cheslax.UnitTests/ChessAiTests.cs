using Cheslax.GameEngine.Chess;

namespace Cheslax.UnitTests;

public class ChessAiTests
{
    [Fact]
    public void Starting_position_returns_a_legal_move()
    {
        var position = new ChessPosition();
        var legal = position.LegalMoves();
        Assert.Contains(legal, move => move.From == "e2" && move.To == "e4");
        var chosen = ChessAi.ChooseMove(position, depth: 1, noise: 0, blunderChance: 0, maxNodes: 2_000, maxTimeMs: 200, quiesce: false);
        Assert.Contains(legal, move => move.From == chosen.From && move.To == chosen.To);
    }
}
