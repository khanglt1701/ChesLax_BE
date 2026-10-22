namespace Cheslax.Domain;

public sealed class UserPreference
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Theme { get; set; } = "light";
    public string ChessBoardTheme { get; set; } = "wood";
    public string ChessPieceSet { get; set; } = "fantasy";
    public string OAnQuanTheme { get; set; } = "wood";
    public DateTimeOffset UpdatedAt { get; set; }

    public static UserPreference CreateDefault(string userId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        UpdatedAt = now,
    };
}
