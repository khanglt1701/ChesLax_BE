using Cheslax.Application.Abstractions;
using Cheslax.Application.Play;
using Cheslax.Domain;
using Cheslax.Infrastructure.Identity;
using Cheslax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cheslax.Infrastructure.Play;

public sealed class RatingService(
    CheslaxDbContext db,
    IClock clock,
    IOptions<MatchmakingOptions> options) : IRatingService
{
    private readonly MatchmakingOptions _options = options.Value;

    public async Task ApplyIfNeededAsync(GameMatch match, CancellationToken cancellationToken)
    {
        if (!match.Rated || match.Outcome is null)
        {
            return;
        }

        if (await db.RatingHistories.AnyAsync(entry => entry.MatchId == match.Id, cancellationToken))
        {
            return;
        }

        var p1Result = match.Outcome switch
        {
            MatchOutcome.Player1Win => MatchResultKind.Win,
            MatchOutcome.Player2Win => MatchResultKind.Loss,
            _ => MatchResultKind.Draw,
        };

        if (match.OpponentType == OpponentType.Ai)
        {
            var humanId = SystemBotUser.IsBotId(match.Player1UserId) ? match.Player2UserId : match.Player1UserId;
            var humanIsP1 = humanId == match.Player1UserId;
            var humanResult = humanIsP1
                ? p1Result
                : p1Result == MatchResultKind.Win
                    ? MatchResultKind.Loss
                    : p1Result == MatchResultKind.Loss
                        ? MatchResultKind.Win
                        : MatchResultKind.Draw;
            var aiRating = humanIsP1 ? match.Player2RatingBefore : match.Player1RatingBefore;
            var change = await ApplyOne(match, humanId, SystemBotUser.UserId, AiOpponentPersona.Resolve(match.AiDifficulty).Name, aiRating, humanResult, cancellationToken);
            if (humanIsP1)
            {
                match.Player1RatingAfter = change.NewRating;
                match.Player2RatingAfter = match.Player2RatingBefore;
            }
            else
            {
                match.Player2RatingAfter = change.NewRating;
                match.Player1RatingAfter = match.Player1RatingBefore;
            }

            return;
        }

        var users = await db.Users.AsNoTracking()
            .Where(user => user.Id == match.Player1UserId || user.Id == match.Player2UserId)
            .ToDictionaryAsync(user => user.Id, cancellationToken);
        var change1 = await ApplyOne(
            match,
            match.Player1UserId,
            match.Player2UserId,
            users.GetValueOrDefault(match.Player2UserId)?.DisplayName ?? "Đối thủ",
            match.Player2RatingBefore,
            p1Result,
            cancellationToken);
        var change2 = await ApplyOne(
            match,
            match.Player2UserId,
            match.Player1UserId,
            users.GetValueOrDefault(match.Player1UserId)?.DisplayName ?? "Đối thủ",
            match.Player1RatingBefore,
            p1Result == MatchResultKind.Win ? MatchResultKind.Loss : p1Result == MatchResultKind.Loss ? MatchResultKind.Win : MatchResultKind.Draw,
            cancellationToken);
        match.Player1RatingAfter = change1.NewRating;
        match.Player2RatingAfter = change2.NewRating;
    }

    private async Task<EloChange> ApplyOne(
        GameMatch match,
        string userId,
        string opponentId,
        string opponentName,
        int opponentRating,
        MatchResultKind result,
        CancellationToken cancellationToken)
    {
        if (SystemBotUser.IsBotId(userId))
        {
            return new EloChange(0, 0, 0, 0);
        }

        var rating = await db.PlayerRatings.FirstOrDefaultAsync(
            item => item.UserId == userId && item.GameType == match.GameType,
            cancellationToken);
        if (rating is null)
        {
            rating = PlayerRating.CreateInitial(userId, match.GameType, clock.UtcNow);
            db.PlayerRatings.Add(rating);
        }

        var change = EloCalculator.Compute(rating.Rating, opponentRating, result, _options.KFactor);
        rating.Rating = change.NewRating;
        rating.PeakRating = Math.Max(rating.PeakRating, change.NewRating);
        rating.GamesPlayed += 1;
        rating.Wins += result == MatchResultKind.Win ? 1 : 0;
        rating.Losses += result == MatchResultKind.Loss ? 1 : 0;
        rating.Draws += result == MatchResultKind.Draw ? 1 : 0;
        rating.UpdatedAt = clock.UtcNow;
        var oldRank = RankCatalog.GetByRating(change.OldRating);
        var newRank = RankCatalog.GetByRating(change.NewRating);
        db.RatingHistories.Add(new RatingHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatchId = match.Id,
            GameType = match.GameType,
            Result = result,
            OldRating = change.OldRating,
            NewRating = change.NewRating,
            Delta = change.Delta,
            OldRankId = oldRank.Id,
            NewRankId = newRank.Id,
            OpponentUserId = opponentId,
            OpponentName = opponentName,
            OpponentRating = opponentRating,
            CreatedAt = clock.UtcNow,
        });
        return change;
    }
}
