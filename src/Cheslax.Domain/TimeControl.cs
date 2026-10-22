namespace Cheslax.Domain;

public sealed record TimeControl(int BaseSeconds, int IncrementSeconds)
{
    public static TimeControl Off { get; } = new(0, 0);

    public static IReadOnlyList<TimeControl> Presets { get; } =
    [
        new(60, 0),
        new(180, 0),
        new(180, 2),
        new(300, 0),
        new(600, 0),
    ];

    public bool IsTimed => BaseSeconds > 0;
    public int BaseMilliseconds => BaseSeconds * 1000;
    public int IncrementMilliseconds => IncrementSeconds * 1000;
    public string Id => IsTimed ? $"{BaseSeconds / 60}+{IncrementSeconds}" : "off";
    public string Label => IsTimed ? $"{BaseSeconds / 60}+{IncrementSeconds}" : "Không giới hạn";

    public static TimeControl Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return Off;
        }

        var parts = value.Split('+', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], out var minutes)
            && int.TryParse(parts[1], out var increment)
            && minutes > 0
            && increment >= 0)
        {
            return new TimeControl(minutes * 60, increment);
        }

        throw new ArgumentException($"Unknown time control '{value}'. Use 1+0, 3+0, 3+2, 5+0, 10+0, or off.", nameof(value));
    }
}
