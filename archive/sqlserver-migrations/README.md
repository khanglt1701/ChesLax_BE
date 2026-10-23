# Archived SQL Server EF Core migrations

These files are the original SQL Server provider migrations from the Cheslax monorepo
(`nvarchar`, `uniqueidentifier`, `rowversion`, `bit`, identity columns).

They are **not compiled** and **must not** be applied to PostgreSQL.

PostgreSQL uses a clean baseline in
`src/Cheslax.Infrastructure/Persistence/Migrations`.

If a local SQL Server `Cheslax` database still has data you need, export it before
switching connection strings. Changing the provider does not move data.
