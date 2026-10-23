using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cheslax.Infrastructure.Persistence;

public sealed class CheslaxDbContextFactory : IDesignTimeDbContextFactory<CheslaxDbContext>
{
    public CheslaxDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? Environment.GetEnvironmentVariable("CONNECTIONSTRINGS_DEFAULT")
            ?? "Host=127.0.0.1;Port=5432;Database=cheslax;Username=cheslax;Password=cheslax";

        var options = new DbContextOptionsBuilder<CheslaxDbContext>();
        options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(CheslaxDbContext).Assembly.FullName));
        return new CheslaxDbContext(options.Options);
    }
}
