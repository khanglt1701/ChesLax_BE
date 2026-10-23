using Cheslax.Application.Abstractions;
using Cheslax.Application.Auth;
using Cheslax.Application.Catalog;
using Cheslax.Application.Play;
using Cheslax.Domain;
using Cheslax.Infrastructure.Catalog;
using Cheslax.Infrastructure.Identity;
using Cheslax.Infrastructure.Play;
using Cheslax.Infrastructure.Options;
using Cheslax.Infrastructure.Persistence;
using Cheslax.Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cheslax.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var database = new DatabaseOptions
        {
            Provider = configuration["Database:Provider"] ?? "SqlServer",
        };
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(database);
        services.Configure<MatchmakingOptions>(configuration.GetSection(MatchmakingOptions.SectionName));
        services.AddScoped<IAuthService, IdentityAuthService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<IPreferenceService, PreferenceService>();
        services.AddScoped<IPlayNotifier, NullPlayNotifier>();
        services.AddScoped<IAIOpponentService, AiOpponentService>();
        services.AddScoped<IHumanMatchmakingService, HumanMatchmakingService>();
        services.AddScoped<IRatingService, RatingService>();
        services.AddScoped<IPlayService, PlayService>();
        services.AddScoped<IMatchmakingService, MatchmakingService>();

        services.AddDbContext<CheslaxDbContext>(options =>
        {
            if (database.IsNpgsql)
            {
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(CheslaxDbContext).Assembly.FullName);
                    npgsql.CommandTimeout(30);
                });
            }
            else if (database.IsSqlite)
            {
                options.UseSqlite(connectionString, sqlite =>
                {
                    sqlite.MigrationsAssembly(typeof(CheslaxDbContext).Assembly.FullName);
                    sqlite.CommandTimeout(30);
                });
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly(typeof(CheslaxDbContext).Assembly.FullName);
                    sql.CommandTimeout(30);
                });
            }
        });

        services
            .AddIdentity<User, Role>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Lockout.MaxFailedAccessAttempts = 8;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            })
            .AddEntityFrameworkStores<CheslaxDbContext>()
            .AddDefaultTokenProviders();

        services.AddHealthChecks()
            .AddDbContextCheck<CheslaxDbContext>("database");

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CheslaxDbContext>();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseOptions>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (database.IsSqlite)
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
        else if (environment.IsDevelopment())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        await SystemBotUser.EnsureAsync(users, cancellationToken);
    }
}
