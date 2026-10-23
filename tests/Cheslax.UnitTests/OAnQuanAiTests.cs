using Cheslax.GameEngine.OAnQuan;

namespace Cheslax.UnitTests;

public class OAnQuanAiTests
{
    [Fact]
    public void Opening_move_is_a_legal_sow()
    {
        var state = OAnQuanRules.CreateInitialState();
        var legal = OAnQuanAi.LegalMoves(state);
        Assert.Equal(10, legal.Count);
        var chosen = OAnQuanAi.ChooseMove(state, searchDepth: 1, blunderChance: 0);
        Assert.Contains(legal, move => move.Pit == chosen.Pit && move.Direction == chosen.Direction);
    }
}
