namespace Cheslax.Domain;

public sealed record AiOpponentPersona(string Name, string AvatarId)
{
    public const string DefaultName = "Lan Chi";
    public const string DefaultAvatarId = "seal-4";

    public static AiOpponentPersona Default { get; } = new(DefaultName, DefaultAvatarId);

    public static AiOpponentPersona Resolve(string? difficultyId) => difficultyId switch
    {
        "beginner" => new("Minh An", "seal-2"),
        "easy" => new("Tuấn Kiệt", "seal-3"),
        "normal" or "medium" => new("Lan Chi", "seal-4"),
        "hard" => new("Quang Huy", "seal-5"),
        "expert" => new("Thu Hà", "seal-6"),
        "advanced" => new("Bảo Long", "seal-7"),
        _ => Default,
    };
}
