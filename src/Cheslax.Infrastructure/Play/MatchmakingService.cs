using Cheslax.Application.Abstractions;
using Cheslax.Application.Common;
using Cheslax.Application.Play;
using Cheslax.Domain;
using Cheslax.Infrastructure.Identity;
using Cheslax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cheslax.Infrastructure.Play;

public sealed class MatchmakingService(
    CheslaxDbContext db,
    IPlayService play,
    IPlayNotifier notifier,
    IClock clock,
    IOptions<MatchmakingOptions> options) : IMatchmakingService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly MatchmakingOptions _options = options.Value;

    public async Task<QueueStatusDto> EnqueueAsync(string userId, string gameId, string? connectionId, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw new CheslaxException("NOT_FOUND", "Không tìm thấy tài khoản.", 404);
        if (user.IsGuest)
        {
            throw new CheslaxException("LOGIN_REQUIRED", "Chế độ xếp hạng cần tài khoản đã đăng nhập.", 401);
        }

        var game = GameTypeIds.Parse(gameId);
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (await db.MatchmakingTickets.AnyAsync(
                    ticket => ticket.UserId == userId && ticket.Status == MatchmakingTicketStatus.Queued,
                    cancellationToken))
            {
                throw new CheslaxException("ALREADY_QUEUED", "Bạn đang trong hàng chờ.", 409);
            }

            if (await db.ActiveMatchLocks.AnyAsync(item => item.UserId == userId, cancellationToken)
                || await db.GameRoomSeats.AnyAsync(
                    seat => seat.UserId == userId && seat.Room != null
                        && (seat.Room.Status == MatchStatus.Waiting || seat.Room.Status == MatchStatus.Playing),
                    cancellationToken))
            {
                throw new CheslaxException("ALREADY_IN_MATCH", "Bạn đang ở trong một ván đấu.", 409);
            }

            var rating = await db.PlayerRatings.FirstOrDefaultAsync(
                item => item.UserId == userId && item.GameType == game,
                cancellationToken);
            if (rating is null)
            {
                rating = PlayerRating.CreateInitial(userId, game, clock.UtcNow);
                db.PlayerRatings.Add(rating);
                await db.SaveChangesAsync(cancellationToken);
            }

            db.MatchmakingTickets.Add(new MatchmakingTicket
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameType = game,
                RatingAtEnqueue = rating.Rating,
                EnqueuedAt = clock.UtcNow,
                Status = MatchmakingTicketStatus.Queued,
                ConnectionId = connectionId,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            Gate.Release();
        }

        var status = (await GetStatusAsync(userId, cancellationToken))!;
        await notifier.QueueUpdatedAsync(userId, status, cancellationToken);
        await MatchOnceAsync(cancellationToken);
        return (await GetStatusAsync(userId, cancellationToken)) ?? status;
    }

    public async Task CancelAsync(string userId, CancellationToken cancellationToken)
    {
        var tickets = await db.MatchmakingTickets
            .Where(ticket => ticket.UserId == userId && ticket.Status == MatchmakingTicketStatus.Queued)
            .ToListAsync(cancellationToken);
        foreach (var ticket in tickets)
        {
            ticket.Status = MatchmakingTicketStatus.Cancelled;
            ticket.ResolvedAt = clock.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<QueueStatusDto?> GetStatusAsync(string userId, CancellationToken cancellationToken)
    {
        var queuedTickets = await db.MatchmakingTickets
            .Where(item => item.UserId == userId && item.Status == MatchmakingTicketStatus.Queued)
            .ToListAsync(cancellationToken);
        var ticket = queuedTickets.OrderByDescending(item => item.EnqueuedAt).FirstOrDefault();
        if (ticket is not null)
        {
            var waited = clock.UtcNow - ticket.EnqueuedAt;
            return new QueueStatusDto(
                "searching",
                ticket.GameType.ToId(),
                ticket.RatingAtEnqueue,
                (int)Math.Max(0, waited.TotalSeconds),
                MatchmakingPolicy.AllowedSpread(waited, _options),
                ticket.EnqueuedAt);
        }

        var matchedTickets = await db.MatchmakingTickets
            .Where(item => item.UserId == userId && item.Status == MatchmakingTicketStatus.Matched && item.MatchId != null)
            .ToListAsync(cancellationToken);
        var matched = matchedTickets.OrderByDescending(item => item.ResolvedAt).FirstOrDefault();
        if (matched?.MatchId is null) return null;
        var room = await db.GameRooms.AsNoTracking()
            .FirstOrDefaultAsync(item => item.CurrentMatchId == matched.MatchId, cancellationToken);
        if (room is null) return null;
        return new QueueStatusDto(
            "matched",
            matched.GameType.ToId(),
            matched.RatingAtEnqueue,
            0,
            0,
            matched.EnqueuedAt,
            new MatchFoundDto(matched.MatchId.Value, room.Code, room.GameType.ToId(), "ranked"));
    }

    public async Task MatchOnceAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var now = clock.UtcNow;
            var queued = (await db.MatchmakingTickets
                    .Where(ticket => ticket.Status == MatchmakingTicketStatus.Queued)
                    .ToListAsync(cancellationToken))
                .OrderBy(ticket => ticket.EnqueuedAt)
                .ToList();
            var busy = (await db.ActiveMatchLocks.Select(item => item.UserId).ToListAsync(cancellationToken)).ToHashSet();
            var consumed = new HashSet<Guid>();
            foreach (var seeker in queued)
            {
                if (consumed.Contains(seeker.Id) || busy.Contains(seeker.UserId)) continue;
                var spread = MatchmakingPolicy.AllowedSpread(now - seeker.EnqueuedAt, _options);
                var opponent = MatchmakingPolicy.FindClosestOpponent(
                    seeker,
                    queued.Where(item => !consumed.Contains(item.Id)).ToList(),
                    spread,
                    busy);
                if (opponent is null) continue;

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
                var found = new MatchFoundDto(room.MatchId ?? Guid.Empty, room.Code, room.Game, "ranked");
                await notifier.MatchFoundAsync(seeker.UserId, found, cancellationToken);
                await notifier.MatchFoundAsync(opponent.UserId, found, cancellationToken);
            }
        }
        finally
        {
            Gate.Release();
        }
    }
}
