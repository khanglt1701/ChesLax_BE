using Cheslax.Domain;
using Cheslax.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CheslaxDbContext).Assembly);
    }
}
