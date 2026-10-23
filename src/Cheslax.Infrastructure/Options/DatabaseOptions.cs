namespace Cheslax.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Npgsql (PostgreSQL) is the default. Sqlite remains for automated tests.</summary>
    public string Provider { get; set; } = "Npgsql";

    /// <summary>
    /// When true, apply EF Core migrations at startup. Development defaults to true.
    /// Production should apply migrations with <c>--migrate</c> (or stay false) so replicas do not race.
    /// </summary>
    public bool ApplyMigrations { get; set; }

    public bool IsSqlServer =>
        string.Equals(Provider, "SqlServer", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Provider, "MSSQL", StringComparison.OrdinalIgnoreCase);

    public bool IsNpgsql =>
        string.Equals(Provider, "Npgsql", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Provider, "Postgres", StringComparison.OrdinalIgnoreCase);

    public bool IsSqlite => string.Equals(Provider, "Sqlite", StringComparison.OrdinalIgnoreCase);
}
