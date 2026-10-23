namespace Cheslax.Api.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] Origins { get; set; } = ["http://localhost:5173", "http://127.0.0.1:5173"];

    public static string[] ParseOrigins(IConfiguration configuration, string[] fallback)
    {
        var raw = configuration[$"{SectionName}:Origins"];
        if (!string.IsNullOrWhiteSpace(raw) && raw.Contains("://", StringComparison.Ordinal))
        {
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var fromArray = configuration.GetSection($"{SectionName}:Origins").Get<string[]>();
        if (fromArray is { Length: > 0 })
        {
            return fromArray.Where(origin => !string.IsNullOrWhiteSpace(origin)).ToArray();
        }

        return fallback;
    }
}
