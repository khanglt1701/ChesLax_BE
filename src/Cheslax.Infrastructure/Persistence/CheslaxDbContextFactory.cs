using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cheslax.Infrastructure.Persistence;

public sealed class CheslaxDbContextFactory : IDesignTimeDbContextFactory<CheslaxDbContext>
{
    public CheslaxDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CheslaxDbContext>();
        options.UseSqlServer(
            @"Server=.\SQLEXPRESS;Database=Cheslax;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True",
            sql => sql.MigrationsAssembly(typeof(CheslaxDbContext).Assembly.FullName));
        return new CheslaxDbContext(options.Options);
    }
}
