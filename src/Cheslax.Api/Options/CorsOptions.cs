namespace Cheslax.Api.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] Origins { get; set; } = ["http://localhost:5173", "http://127.0.0.1:5173"];
}
