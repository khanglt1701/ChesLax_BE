using Cheslax.Domain;

namespace Cheslax.UnitTests;

public class GameTypeTests
{
    [Theory]
    [InlineData("chess", GameType.Chess)]
    [InlineData("o-an-quan", GameType.OAnQuan)]
    public void Parses_canonical_ids(string id, GameType expected)
    {
        Assert.True(GameTypeIds.TryParse(id, out var gameType));
        Assert.Equal(expected, gameType);
        Assert.Equal(id, gameType.ToId());
    }

    [Fact]
    public void Rejects_unknown_ids()
    {
        Assert.False(GameTypeIds.TryParse("sudoku", out _));
        Assert.Throws<ArgumentException>(() => GameTypeIds.Parse("caro"));
    }
}
