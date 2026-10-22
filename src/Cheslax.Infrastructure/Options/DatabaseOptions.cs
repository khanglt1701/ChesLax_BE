namespace Cheslax.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>SqlServer for local SQL Express; Sqlite for automated tests; Npgsql optional.</summary>
    public string Provider { get; set; } = "SqlServer";

    public bool IsSqlServer =>
        string.Equals(Provider, "SqlServer", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Provider, "MSSQL", StringComparison.OrdinalIgnoreCase);

    public bool IsNpgsql => string.Equals(Provider, "Npgsql", StringComparison.OrdinalIgnoreCase);

    public bool IsSqlite => string.Equals(Provider, "Sqlite", StringComparison.OrdinalIgnoreCase);
}
