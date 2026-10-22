using Cheslax.Application.Catalog;
using Cheslax.Application.Common;
using Cheslax.Domain;
using Cheslax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cheslax.Infrastructure.Catalog;

public sealed class LeaderboardService(CheslaxDbContext db) : ILeaderboardService
{
    public async Task<LeaderboardDto> GetAsync(
        string gameId,
        int page,
        int pageSize,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        if (!GameTypeIds.TryParse(gameId, out var gameType))
        {
            throw new CheslaxException("INVALID", "Trò chơi không hợp lệ.", 400);
        }
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query =
            from rating in db.PlayerRatings.AsNoTracking()
            join user in db.Users.AsNoTracking() on rating.UserId equals user.Id
            where rating.GameType == gameType
            orderby rating.Rating descending, rating.GamesPlayed descending, user.NormalizedUserName
            select new
            {
                user.UserName,
                user.DisplayName,
                user.AvatarId,
                user.Id,
                rating.Rating,
                rating.GamesPlayed,
            };

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select((row, index) => new LeaderboardEntryDto(
                Rank: (page - 1) * pageSize + index + 1,
                Username: row.UserName!,
                DisplayName: row.DisplayName,
                AvatarId: row.AvatarId,
                Rating: row.Rating,
                GamesPlayed: row.GamesPlayed))
            .ToList();

        LeaderboardEntryDto? you = null;
        if (!string.IsNullOrEmpty(currentUserId))
        {
            var mine = await (
                from rating in db.PlayerRatings.AsNoTracking()
                join user in db.Users.AsNoTracking() on rating.UserId equals user.Id
                where rating.GameType == gameType && rating.UserId == currentUserId
                select new { user.UserName, user.DisplayName, user.AvatarId, rating.Rating, rating.GamesPlayed }
            ).FirstOrDefaultAsync(cancellationToken);

            if (mine is not null)
            {
                var better = await db.PlayerRatings.AsNoTracking()
                    .CountAsync(
                        rating => rating.GameType == gameType
                            && (rating.Rating > mine.Rating
                                || (rating.Rating == mine.Rating && rating.GamesPlayed > mine.GamesPlayed)),
                        cancellationToken);
                you = new LeaderboardEntryDto(
                    better + 1,
                    mine.UserName!,
                    mine.DisplayName,
                    mine.AvatarId,
                    mine.Rating,
                    mine.GamesPlayed);
            }
        }

        return new LeaderboardDto(gameType.ToId(), items, page, pageSize, total, you);
    }
}
