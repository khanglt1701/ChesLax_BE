using Cheslax.Domain;
using Cheslax.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cheslax.Infrastructure.Persistence;

public class CheslaxDbContext(DbContextOptions<CheslaxDbContext> options)
    : IdentityDbContext<User, Role, string, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>(options)
{
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<PlayerRating> PlayerRatings => Set<PlayerRating>();
    public DbSet<RatingHistory> RatingHistories => Set<RatingHistory>();
    public DbSet<GameRoom> GameRooms => Set<GameRoom>();
    public DbSet<GameRoomSeat> GameRoomSeats => Set<GameRoomSeat>();
    public DbSet<GameMatch> GameMatches => Set<GameMatch>();
    public DbSet<MatchMove> MatchMoves => Set<MatchMove>();
    public DbSet<MatchmakingTicket> MatchmakingTickets => Set<MatchmakingTicket>();
    public DbSet<ActiveMatchLock> ActiveMatchLocks => Set<ActiveMatchLock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CheslaxDbContext).Assembly);
        var room = modelBuilder.Entity<GameRoom>();
        foreach (var foreignKey in room.Metadata.GetForeignKeys()
            .Where(key => key.Properties.Any(property => property.Name == nameof(GameRoom.CurrentMatchId)))
            .ToList())
        {
            room.Metadata.RemoveForeignKey(foreignKey);
        }

        if (!UsesSqlite())
        {
            return;
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var property = entityType.FindProperty(nameof(GameRoom.RowVersion));
            if (property is null || property.ClrType != typeof(byte[]))
            {
                continue;
            }

            property.IsConcurrencyToken = false;
            property.ValueGenerated = ValueGenerated.Never;
            property.SetDefaultValue(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });
        }
    }

    private bool UsesSqlite() =>
        (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ?? false)
        || options.Extensions.Any(extension =>
            extension.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));
}
