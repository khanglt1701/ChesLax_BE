using Cheslax.GameEngine.Chess;

namespace Cheslax.UnitTests;

public class ChessRulesTests
{
    [Fact]
    public void Starting_position_allows_e4()
    {
        var position = new ChessPosition();
        Assert.True(position.TryMove("e2", "e4", null, out var next));
        Assert.Equal('b', next!.Position.Turn);
        Assert.Contains("e4", next.San);
    }

    [Fact]
    public void Rejects_illegal_pawn_leap()
    {
        var position = new ChessPosition();
        Assert.False(position.TryMove("e2", "e5", null, out _));
    }

    [Fact]
    public void Detects_scholars_mate()
    {
        var position = new ChessPosition();
        foreach (var (from, to) in new[] { ("e2", "e4"), ("e7", "e5"), ("d1", "h5"), ("b8", "c6"), ("f1", "c4"), ("g8", "f6"), ("h5", "f7") })
        {
            Assert.True(position.TryMove(from, to, null, out var next), $"{from}{to}");
            position = next!.Position;
        }

        Assert.True(position.IsCheckmate);
        Assert.Equal("checkmate", position.Status);
    }
}
