using Cheslax.Application.Abstractions;
using Cheslax.Application.Play;
using Cheslax.Domain;
using Cheslax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cheslax.Infrastructure.Play;

public sealed class HumanMatchmakingService(
    CheslaxDbContext db,
    IPlayService play,
    IPlayNotifier notifier,
    IClock clock,
    IOptions<MatchmakingOptions> options) : IHumanMatchmakingService
{
    private readonly MatchmakingOptions _options = options.Value;

    public async Task MatchQueuedHumansAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var queued = (await db.MatchmakingTickets
                .Where(ticket => ticket.Status == MatchmakingTicketStatus.Queued)
                .ToListAsync(cancellationToken))
            .OrderBy(ticket => ticket.EnqueuedAt)
            .ToList();
        if (queued.Count == 0)
        {
            return;
        }

        var busy = (await db.ActiveMatchLocks.Select(item => item.UserId).ToListAsync(cancellationToken)).ToHashSet();
        var consumed = new HashSet<Guid>();
        var byGame = queued.GroupBy(ticket => ticket.GameType).ToDictionary(group => group.Key, group => group.Count());

        foreach (var seeker in queued)
        {
            if (consumed.Contains(seeker.Id) || busy.Contains(seeker.UserId))
            {
                continue;
            }

            if (byGame.GetValueOrDefault(seeker.GameType) < Math.Max(2, _options.MinimumActiveHumanPlayers)
                && _options.EffectiveRankedMode() == RankedMatchmakingModes.HumanFirstWithAIFallback)
            {
                continue;
            }

            var spread = MatchmakingPolicy.AllowedSpread(now - seeker.EnqueuedAt, _options);
            var opponent = MatchmakingPolicy.FindClosestOpponent(
                seeker,
                queued.Where(item => !consumed.Contains(item.Id)).ToList(),
                spread,
                busy);
            if (opponent is null)
            {
                continue;
            }

            var room = await play.CreateRankedMatchAsync(
                seeker.UserId,
                opponent.UserId,
                seeker.GameType.ToId(),
                seeker.RatingAtEnqueue,
                opponent.RatingAtEnqueue,
                cancellationToken);
            seeker.Status = MatchmakingTicketStatus.Matched;
            opponent.Status = MatchmakingTicketStatus.Matched;
            seeker.MatchId = room.MatchId;
            opponent.MatchId = room.MatchId;
            seeker.ResolvedAt = now;
            opponent.ResolvedAt = now;
            consumed.Add(seeker.Id);
            consumed.Add(opponent.Id);
            busy.Add(seeker.UserId);
            busy.Add(opponent.UserId);
            await db.SaveChangesAsync(cancellationToken);
            var found = new MatchFoundDto(room.MatchId ?? Guid.Empty, room.Code, room.Game, "ranked", "human");
            await notifier.MatchFoundAsync(seeker.UserId, found, cancellationToken);
            await notifier.MatchFoundAsync(opponent.UserId, found, cancellationToken);
        }
    }
}
