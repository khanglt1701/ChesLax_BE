namespace Cheslax.Domain;

public enum GameType
{
    Chess = 1,
    OAnQuan = 2,
}

public static class GameTypeIds
{
    public const string Chess = "chess";
    public const string OAnQuan = "o-an-quan";

    public static string ToId(this GameType gameType) => gameType switch
    {
        GameType.Chess => Chess,
        GameType.OAnQuan => OAnQuan,
        _ => throw new ArgumentOutOfRangeException(nameof(gameType), gameType, "Unknown game type."),
    };

    public static bool TryParse(string? value, out GameType gameType)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case Chess:
                gameType = GameType.Chess;
                return true;
            case OAnQuan:
            case "oanquan":
            case "o ăn quan":
                gameType = GameType.OAnQuan;
                return true;
            default:
                gameType = default;
                return false;
        }
    }

    public static GameType Parse(string? value)
    {
        if (TryParse(value, out var gameType))
        {
            return gameType;
        }

        throw new ArgumentException($"Unknown game type '{value}'. Use '{Chess}' or '{OAnQuan}'.", nameof(value));
    }
}
