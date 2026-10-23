using System.Security.Cryptography;
using Cheslax.Application.Abstractions;
using Cheslax.Application.Common;
using Cheslax.Application.Play;
using Cheslax.Domain;
using Cheslax.GameEngine.Chess;
using Cheslax.GameEngine.OAnQuan;
using Cheslax.Infrastructure.Identity;
using Cheslax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cheslax.Infrastructure.Play;

public sealed class PlayService(
    CheslaxDbContext db,
    IClock clock,
    IPlayNotifier notifier,
    IOptions<MatchmakingOptions> options) : IPlayService
{
    private static readonly char[] RoomAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();
    private readonly MatchmakingOptions _options = options.Value;

    public async Task<RoomPublicDto> CreateCasualRoomAsync(string userId, CreateCasualRoomRequest request, CancellationToken cancellationToken)
    {
        var user = await RequireUser(userId, cancellationToken);
        await EnsureNotBusy(userId, cancellationToken);
        var game = GameTypeIds.Parse(request.GameId);
        var timeControl = TimeControl.Parse(request.TimeControl);
        var now = clock.UtcNow;
        var room = new GameRoom
        {
            Id = Guid.NewGuid(),
            Code = await UniqueCode(cancellationToken),
            GameType = game,
            Mode = MatchMode.Casual,
            Status = MatchStatus.Waiting,
            TimeControlId = timeControl.Id,
            Rated = false,
            HostUserId = user.Id,
            CreatedAt = now,
            LastActivityAt = now,
            StateJson = PlayJson.Serialize(CreateInitialState(game, timeControl, now)),
        };
        var side = PickHostSide(game, request.ColorPref);
        room.Seats.Add(MakeSeat(room.Id, user.Id, 1, side, now));
        db.GameRooms.Add(room);
        await db.SaveChangesAsync(cancellationToken);
        return await ToPublic(room.Code, user.Id, cancellationToken);
    }

    public async Task<RoomPublicDto> JoinRoomAsync(string userId, string code, CancellationToken cancellationToken)
    {
        var user = await RequireUser(userId, cancellationToken);
        var room = await LoadRoom(code, cancellationToken);
        var existing = room.Seats.FirstOrDefault(seat => seat.UserId == userId);
        if (existing is not null)
        {
            existing.Connected = true;
            existing.LastDisconnectedAt = null;
            room.DisconnectDeadline = AllConnected(room) ? null : room.DisconnectDeadline;
            room.LastActivityAt = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await notifier.RoomUpdatedAsync(room.Code, SeatIds(room), cancellationToken);
            return await ToPublic(room.Code, userId, cancellationToken);
        }

        if (room.Status is MatchStatus.Finished or MatchStatus.Abandoned)
        {
            throw new CheslaxException("ROOM_ENDED", "Phòng đã kết thúc.", 409);
        }

        if (room.Seats.Count >= 2)
        {
            throw new CheslaxException("ROOM_FULL", "Phòng đã đủ người.", 409);
        }

        await EnsureNotBusy(userId, cancellationToken);
        var now = clock.UtcNow;
        var host = room.Seats.Single();
        var guestSeat = MakeSeat(room.Id, user.Id, 2, OtherSide(room.GameType, host.Side), now);
        room.Seats.Add(guestSeat);
        db.GameRoomSeats.Add(guestSeat);
        db.Entry(guestSeat).State = EntityState.Added;
        await StartMatch(room, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.RoomUpdatedAsync(room.Code, SeatIds(room), cancellationToken);
        return await ToPublic(room.Code, userId, cancellationToken);
    }

    public Task<RoomPublicDto> GetRoomAsync(string userId, string code, CancellationToken cancellationToken) =>
        ToPublic(code, userId, cancellationToken);

    public async Task LeaveRoomAsync(string userId, string code, CancellationToken cancellationToken)
    {
        var room = await LoadRoom(code, cancellationToken);
        var seat = room.Seats.FirstOrDefault(item => item.UserId == userId)
            ?? throw new CheslaxException("UNAUTHORIZED", "Bạn không thuộc phòng này.", 403);
        if (room.Status == MatchStatus.Waiting)
        {
            db.GameRoomSeats.Remove(seat);
            if (room.Seats.Count <= 1)
            {
                room.Status = MatchStatus.Abandoned;
            }

            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (room.Status == MatchStatus.Playing)
        {
            seat.Connected = false;
            seat.LastDisconnectedAt = clock.UtcNow;
            room.DisconnectDeadline = clock.UtcNow.AddSeconds(_options.ReconnectGraceSeconds);
            await db.SaveChangesAsync(cancellationToken);
            await notifier.RoomUpdatedAsync(room.Code, SeatIds(room), cancellationToken);
        }
    }

    public async Task SetConnectedAsync(string userId, string? connectionId, bool connected, CancellationToken cancellationToken)
    {
        var seats = await db.GameRoomSeats.Include(seat => seat.Room)
            .Where(seat => seat.UserId == userId && seat.Room != null &&
                           (seat.Room.Status == MatchStatus.Waiting || seat.Room.Status == MatchStatus.Playing))
            .ToListAsync(cancellationToken);
        foreach (var seat in seats)
        {
            seat.Connected = connected;
            seat.LastDisconnectedAt = connected ? null : clock.UtcNow;
            if (seat.Room is null) continue;
            if (connected && AllConnected(seat.Room))
            {
                seat.Room.DisconnectDeadline = null;
            }
            else if (!connected && seat.Room.Status == MatchStatus.Playing)
            {
                seat.Room.DisconnectDeadline ??= clock.UtcNow.AddSeconds(_options.ReconnectGraceSeconds);
            }
        }

        var tickets = await db.MatchmakingTickets
            .Where(ticket => ticket.UserId == userId && ticket.Status == MatchmakingTicketStatus.Queued)
            .ToListAsync(cancellationToken);
        foreach (var ticket in tickets)
        {
            ticket.ConnectionId = connected ? connectionId : ticket.ConnectionId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RoomPublicDto> ChessMoveAsync(string userId, string code, ChessMoveRequest request, CancellationToken cancellationToken)
    {
        var room = await LoadRoom(code, cancellationToken);
        var match = await RequirePlaying(room, cancellationToken);
        var seat = RequireSeat(room, userId);
        if (request.Version != room.Version)
        {
            throw new CheslaxException("STALE_STATE", "Trạng thái đã cũ, đang đồng bộ lại.", 409);
        }

        SyncClocks(room, match);
        var state = PlayJson.Parse(room.StateJson);
        var position = new ChessPosition(state.ChessFen ?? ChessPosition.StartingFen);
        if (seat.Side.Length == 0 || seat.Side[0] != position.Turn)
        {
            throw new CheslaxException("NOT_YOUR_TURN", "Không phải lượt của bạn.", 409);
        }

        if (!position.TryMove(request.From, request.To, ParsePromotion(request.Promotion), out var applied) || applied is null)
        {
            throw new CheslaxException("ILLEGAL_MOVE", "Nước đi không hợp lệ.", 400);
        }

        state.ChessFen = applied.Position.Fen;
        state.ChessHistory = applied.Position.History;
        state.LastFrom = applied.From;
        state.LastTo = applied.To;
        ApplyClockIncrement(state, seat.Side);
        state.ClockRunning = applied.Position.Turn.ToString();
        state.ClockUpdatedAt = clock.UtcNow;
        room.StateJson = PlayJson.Serialize(state);
        room.Version += 1;
        room.LastActivityAt = clock.UtcNow;
        db.MatchMoves.Add(new MatchMove
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            Sequence = match.Moves.Count + 1,
            PlayerUserId = userId,
            Payload = $"{applied.From}{applied.To}{applied.Promotion}",
            ClockMsRemaining = seat.Side == "w" ? state.WhiteMs : state.BlackMs,
            PlayedAt = clock.UtcNow,
        });
        if (applied.Position.IsGameOver)
        {
            FinishFromChess(room, match, applied.Position);
            await FinalizeRatings(room, match, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await notifier.RoomUpdatedAsync(room.Code, SeatIds(room), cancellationToken);
        return await ToPublic(room.Code, userId, cancellationToken);
    }

    public async Task<RoomPublicDto> OAnQuanMoveAsync(string userId, string code, OAnQuanMoveRequest request, CancellationToken cancellationToken)
    {
        var room = await LoadRoom(code, cancellationToken);
        var match = await RequirePlaying(room, cancellationToken);
        var seat = RequireSeat(room, userId);
        if (request.Version != room.Version)
        {
            throw new CheslaxException("STALE_STATE", "Trạng thái đã cũ, đang đồng bộ lại.", 409);
        }

        if (!int.TryParse(seat.Side, out var playerId))
        {
            throw new CheslaxException("UNAUTHORIZED", "Ghế chơi không hợp lệ.", 403);
        }

        var direction = request.Direction is "cw" or "clockwise"
            ? OAnQuanDirection.Clockwise
            : request.Direction is "ccw" or "counterclockwise"
                ? OAnQuanDirection.CounterClockwise
                : throw new CheslaxException("INVALID", "Hướng rải không hợp lệ.", 400);
        var state = PlayJson.Parse(room.StateJson);
        var board = state.OAnQuan ?? OAnQuanRules.CreateInitialState();
        try
        {
            var applied = OAnQuanRules.ApplyMoveForPlayer(board, request.Pit, direction, playerId);
            state.OAnQuan = applied.State;
            state.OAnQuanHistory.Add(applied.Description);
            state.LastPit = request.Pit;
            state.LastDirection = request.Direction;
            state.LastEvents = OAnQuanPublicMapper.ToPublicEvents(applied.Events);
            room.StateJson = PlayJson.Serialize(state);
            room.Version += 1;
            room.LastActivityAt = clock.UtcNow;
            db.MatchMoves.Add(new MatchMove
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                Sequence = match.Moves.Count + 1,
                PlayerUserId = userId,
                Payload = $"{request.Pit}:{request.Direction}",
                PlayedAt = clock.UtcNow,
            });
            if (applied.State.Status == "ended")
            {
                FinishFromOAnQuan(room, match, applied.State);
                await FinalizeRatings(room, match, cancellationToken);
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new CheslaxException("ILLEGAL_MOVE", ex.Message, 400);
        }

        await db.SaveChangesAsync(cancellationToken);
        await notifier.RoomUpdatedAsync(room.Code, SeatIds(room), cancellationToken);
        return await ToPublic(room.Code, userId, cancellationToken);
    }

    public async Task<RoomPublicDto> ResignAsync(string userId, string code, CancellationToken cancellationToken)
    {
        var room = await LoadRoom(code, cancellationToken);
        var match = await RequirePlaying(room, cancellationToken);
        var seat = RequireSeat(room, userId);
        var winner = OtherSide(room.GameType, seat.Side);
        await CompleteAsync(room, match, OutcomeForWinner(match, winner), ResultReason.Resignation, "Đối thủ đã đầu hàng", rate: room.Rated, cancellationToken);
        await notifier.RoomUpdatedAsync(room.Code, SeatIds(room), cancellationToken);
        return await ToPublic(room.Code, userId, cancellationToken);
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var rooms = await db.GameRooms
            .Include(room => room.Seats)
            .Include(room => room.Matches)
            .Where(room => room.Status == MatchStatus.Playing)
            .ToListAsync(cancellationToken);
        foreach (var room in rooms)
        {
            var match = room.Matches.FirstOrDefault(item => item.Id == room.CurrentMatchId);
            if (match is null || match.Status != MatchStatus.Playing) continue;
            var changed = SyncClocks(room, match);
            if (match.Status == MatchStatus.Finished)
            {
                await FinalizeRatings(room, match, cancellationToken);
                changed = true;
            }
            else if (room.DisconnectDeadline is not null && now >= room.DisconnectDeadline)
            {
                var disconnected = room.Seats.Where(seat => !seat.Connected).ToList();
                if (disconnected.Count == 1)
                {
                    var winner = OtherSide(room.GameType, disconnected[0].Side);
                    await CompleteAsync(
                        room,
                        match,
                        OutcomeForWinner(match, winner),
                        ResultReason.Abandonment,
                        disconnected[0].LastDisconnectedAt is null ? "Đối thủ đã rời phòng" : "Đối thủ mất kết nối quá lâu",
                        rate: room.Rated,
                        cancellationToken);
                    changed = true;
                }
                else if (disconnected.Count >= 2)
                {
                    await CompleteAsync(room, match, MatchOutcome.Draw, ResultReason.Abandonment, "Cả hai người đã mất kết nối", rate: false, cancellationToken);
                    changed = true;
                }
            }

            if (changed)
            {
                await db.SaveChangesAsync(cancellationToken);
                await notifier.RoomUpdatedAsync(room.Code, SeatIds(room), cancellationToken);
            }
        }
    }

    public async Task<RoomPublicDto> CreateRankedMatchAsync(string player1Id, string player2Id, string gameId, int rating1, int rating2, CancellationToken cancellationToken)
    {
        var player1 = await RequireUser(player1Id, cancellationToken);
        var player2 = await RequireUser(player2Id, cancellationToken);
        var game = GameTypeIds.Parse(gameId);
        var now = clock.UtcNow;
        var timeControl = TimeControl.Off;
        var room = new GameRoom
        {
            Id = Guid.NewGuid(),
            Code = await UniqueCode(cancellationToken),
            GameType = game,
            Mode = MatchMode.Ranked,
            Status = MatchStatus.Waiting,
            TimeControlId = timeControl.Id,
            Rated = true,
            HostUserId = player1.Id,
            CreatedAt = now,
            LastActivityAt = now,
            StateJson = PlayJson.Serialize(CreateInitialState(game, timeControl, now)),
        };
        var hostSide = PickHostSide(game, "random");
        room.Seats.Add(MakeSeat(room.Id, player1.Id, 1, hostSide, now));
        room.Seats.Add(MakeSeat(room.Id, player2.Id, 2, OtherSide(game, hostSide), now));
        db.GameRooms.Add(room);
        await StartMatch(room, cancellationToken, rating1, rating2);
        await db.SaveChangesAsync(cancellationToken);
        return await ToPublic(room.Code, player1.Id, cancellationToken);
    }

    private async Task<GameMatch> StartMatch(GameRoom room, CancellationToken cancellationToken, int? rating1 = null, int? rating2 = null)
    {
        var ordered = room.Seats.OrderBy(seat => seat.SeatNumber).ToList();
        var p1 = ordered[0];
        var p2 = ordered[1];
        var now = clock.UtcNow;
        rating1 ??= await RatingOf(p1.UserId, room.GameType, cancellationToken);
        rating2 ??= await RatingOf(p2.UserId, room.GameType, cancellationToken);
        var match = new GameMatch
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            GameType = room.GameType,
            Mode = room.Mode,
            Status = MatchStatus.Playing,
            Rated = room.Rated,
            TimeControlId = room.TimeControlId,
            Player1UserId = p1.UserId,
            Player2UserId = p2.UserId,
            Player1Side = p1.Side,
            Player2Side = p2.Side,
            Player1RatingBefore = rating1.Value,
            Player2RatingBefore = rating2.Value,
            CreatedAt = now,
            StartedAt = now,
        };
        db.GameMatches.Add(match);
        room.Matches.Add(match);
        db.Entry(match).State = EntityState.Added;
        room.Status = MatchStatus.Playing;
        room.LastActivityAt = now;
        db.ActiveMatchLocks.AddRange(
            new ActiveMatchLock { UserId = p1.UserId, MatchId = match.Id, RoomId = room.Id, GameType = room.GameType, Mode = room.Mode, CreatedAt = now },
            new ActiveMatchLock { UserId = p2.UserId, MatchId = match.Id, RoomId = room.Id, GameType = room.GameType, Mode = room.Mode, CreatedAt = now });
        var state = PlayJson.Parse(room.StateJson);
        if (state.WhiteMs > 0 || state.BlackMs > 0)
        {
            state.ClockRunning = room.GameType == GameType.Chess ? "w" : null;
            state.ClockUpdatedAt = now;
            room.StateJson = PlayJson.Serialize(state);
        }

        await db.SaveChangesAsync(cancellationToken);
        room.CurrentMatchId = match.Id;
        return match;
    }

    private void FinishFromChess(GameRoom room, GameMatch match, ChessPosition position)
    {
        if (position.IsCheckmate)
        {
            var winner = position.Turn == 'w' ? "b" : "w";
            CompleteInMemory(room, match, OutcomeForWinner(match, winner), ResultReason.Checkmate, "Chiếu hết", room.Rated);
            return;
        }

        var reason = position.Status switch
        {
            "stalemate" => ResultReason.Stalemate,
            "draw-insufficient" => ResultReason.InsufficientMaterial,
            "draw-threefold" => ResultReason.ThreefoldRepetition,
            "draw-fifty" => ResultReason.FiftyMoveRule,
            _ => ResultReason.AgreedDraw,
        };
        CompleteInMemory(room, match, MatchOutcome.Draw, reason, "Hòa cờ", room.Rated);
    }

    private void FinishFromOAnQuan(GameRoom room, GameMatch match, OAnQuanState state)
    {
        if (state.Winner == "draw" || state.Winner is null)
        {
            CompleteInMemory(room, match, MatchOutcome.Draw, ResultReason.OAnQuanScore, "Hòa điểm", room.Rated);
            return;
        }

        var reason = state.EndReason == OAnQuanEndReason.CannotResupply ? ResultReason.CannotResupply : ResultReason.OAnQuanScore;
        CompleteInMemory(room, match, OutcomeForWinner(match, state.Winner), reason, "Ván ô ăn quan kết thúc", room.Rated);
    }

    private async Task CompleteAsync(
        GameRoom room,
        GameMatch match,
        MatchOutcome outcome,
        ResultReason reason,
        string label,
        bool rate,
        CancellationToken cancellationToken)
    {
        CompleteInMemory(room, match, outcome, reason, label, rate);
        await FinalizeRatings(room, match, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task FinalizeRatings(GameRoom room, GameMatch match, CancellationToken cancellationToken)
    {
        await ApplyRatingsIfNeeded(room, match, cancellationToken);
        var locks = db.ActiveMatchLocks.Local.Where(item => item.MatchId == match.Id).ToList();
        if (locks.Count == 0)
        {
            locks = await db.ActiveMatchLocks.Where(item => item.MatchId == match.Id).ToListAsync(cancellationToken);
        }

        db.ActiveMatchLocks.RemoveRange(locks);
        _ = room;
    }

    private void CompleteInMemory(GameRoom room, GameMatch match, MatchOutcome outcome, ResultReason reason, string label, bool rate)
    {
        if (match.Status is MatchStatus.Finished or MatchStatus.Abandoned)
        {
            return;
        }

        match.Status = MatchStatus.Finished;
        match.Outcome = outcome;
        match.ResultReason = reason;
        match.FinishedAt = clock.UtcNow;
        match.StateSnapshot = label;
        room.Status = MatchStatus.Finished;
        room.DisconnectDeadline = null;
        var state = PlayJson.Parse(room.StateJson);
        state.ClockRunning = null;
        room.StateJson = PlayJson.Serialize(state);
        if (!rate)
        {
            match.Rated = false;
        }
    }

    private async Task ApplyRatingsIfNeeded(GameRoom room, GameMatch match, CancellationToken cancellationToken)
    {
        if (!match.Rated || match.Outcome is null)
        {
            return;
        }

        var already = await db.RatingHistories.AnyAsync(entry => entry.MatchId == match.Id, cancellationToken);
        if (already)
        {
            return;
        }

        var p1Result = match.Outcome switch
        {
            MatchOutcome.Player1Win => MatchResultKind.Win,
            MatchOutcome.Player2Win => MatchResultKind.Loss,
            _ => MatchResultKind.Draw,
        };
        var (change1, change2) = EloCalculator.ComputePair(
            match.Player1RatingBefore,
            match.Player2RatingBefore,
            p1Result,
            _options.KFactor);
        var users = await db.Users.AsNoTracking()
            .Where(user => user.Id == match.Player1UserId || user.Id == match.Player2UserId)
            .ToDictionaryAsync(user => user.Id, cancellationToken);
        await ApplyOneRating(match, match.Player1UserId, match.Player2UserId, users.GetValueOrDefault(match.Player2UserId)?.DisplayName ?? "Đối thủ", change1, p1Result, cancellationToken);
        await ApplyOneRating(
            match,
            match.Player2UserId,
            match.Player1UserId,
            users.GetValueOrDefault(match.Player1UserId)?.DisplayName ?? "Đối thủ",
            change2,
            p1Result == MatchResultKind.Win ? MatchResultKind.Loss : p1Result == MatchResultKind.Loss ? MatchResultKind.Win : MatchResultKind.Draw,
            cancellationToken);
        match.Player1RatingAfter = change1.NewRating;
        match.Player2RatingAfter = change2.NewRating;
    }

    private async Task ApplyOneRating(
        GameMatch match,
        string userId,
        string opponentId,
        string opponentName,
        EloChange change,
        MatchResultKind result,
        CancellationToken cancellationToken)
    {
        var rating = await db.PlayerRatings.FirstOrDefaultAsync(
            item => item.UserId == userId && item.GameType == match.GameType,
            cancellationToken);
        if (rating is null)
        {
            rating = PlayerRating.CreateInitial(userId, match.GameType, clock.UtcNow);
            db.PlayerRatings.Add(rating);
        }

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
            OpponentRating = userId == match.Player1UserId ? match.Player2RatingBefore : match.Player1RatingBefore,
            CreatedAt = clock.UtcNow,
        });
    }

    private bool SyncClocks(GameRoom room, GameMatch match)
    {
        var state = PlayJson.Parse(room.StateJson);
        if (state.ClockRunning is null || state.ClockUpdatedAt is null || (state.WhiteMs ?? 0) <= 0)
        {
            return false;
        }

        var elapsed = (int)Math.Max(0, (clock.UtcNow - state.ClockUpdatedAt.Value).TotalMilliseconds);
        if (elapsed <= 0) return false;
        if (state.ClockRunning == "w") state.WhiteMs = Math.Max(0, (state.WhiteMs ?? 0) - elapsed);
        if (state.ClockRunning == "b") state.BlackMs = Math.Max(0, (state.BlackMs ?? 0) - elapsed);
        state.ClockUpdatedAt = clock.UtcNow;
        room.StateJson = PlayJson.Serialize(state);
        if (state.WhiteMs <= 0 || state.BlackMs <= 0)
        {
            var winner = state.WhiteMs <= 0 ? "b" : "w";
            CompleteInMemory(room, match, OutcomeForWinner(match, winner), ResultReason.Timeout, winner == "b" ? "Trắng hết giờ" : "Đen hết giờ", room.Rated);
        }

        return true;
    }

    private static void ApplyClockIncrement(PersistedPlayState state, string side)
    {
        // Increment is encoded in time control id like 3+2. Budget is stored as remaining ms.
        // Casual/ranked currently use the remaining budget only; increment is applied by the client-facing control id at start.
        _ = side;
        _ = state;
    }

    private async Task<RoomPublicDto> ToPublic(string code, string userId, CancellationToken cancellationToken)
    {
        var room = await LoadRoom(code, cancellationToken);
        var match = room.Matches.FirstOrDefault(item => item.Id == room.CurrentMatchId);
        var users = await db.Users.AsNoTracking()
            .Where(user => room.Seats.Select(seat => seat.UserId).Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);
        var state = PlayJson.Parse(room.StateJson);
        ChessPublicDto? chess = null;
        if (room.GameType == GameType.Chess)
        {
            var position = new ChessPosition(state.ChessFen ?? ChessPosition.StartingFen);
            chess = new ChessPublicDto(
                position.Fen,
                position.Turn.ToString(),
                state.ChessHistory,
                state.LastFrom is null ? null : new { from = state.LastFrom, to = state.LastTo },
                position.InCheck,
                position.IsGameOver || room.Status == MatchStatus.Finished,
                position.Status,
                state.WhiteMs is null ? null : new { w = state.WhiteMs, b = state.BlackMs, running = state.ClockRunning });
        }

        OAnQuanPublicDto? oaq = null;
        if (room.GameType == GameType.OAnQuan && state.OAnQuan is not null)
        {
            oaq = new OAnQuanPublicDto(
                OAnQuanPublicMapper.ToPublicState(state.OAnQuan),
                state.OAnQuanHistory,
                state.LastPit is null ? null : new { pit = state.LastPit, direction = state.LastDirection },
                state.LastEvents.Cast<object>().ToList());
        }

        var youSeat = room.Seats.FirstOrDefault(seat => seat.UserId == userId);
        RoomResultDto? result = null;
        if (match is { Status: MatchStatus.Finished })
        {
            var change = match.Rated ? ChangeFor(match, userId) : null;
            result = new RoomResultDto(
                match.StateSnapshot ?? "Ván đã kết thúc",
                WinnerSide(match),
                match.Rated,
                change);
        }

        return new RoomPublicDto(
            room.Code,
            room.GameType.ToId(),
            room.Mode == MatchMode.Ranked ? "ranked" : "casual",
            room.Rated,
            MapStatus(room.Status),
            room.Version,
            room.TimeControlId,
            youSeat is null ? null : new { playerId = youSeat.UserId, side = ParseSide(room.GameType, youSeat.Side) },
            room.Seats.Select(seat => new PublicSeatDto(
                seat.UserId,
                users.GetValueOrDefault(seat.UserId)?.DisplayName ?? "Kỳ thủ",
                users.GetValueOrDefault(seat.UserId)?.AvatarId ?? "seal-1",
                seat.Connected,
                ParseSide(room.GameType, seat.Side))).ToList(),
            room.Status == MatchStatus.Waiting,
            chess,
            oaq,
            result,
            room.DisconnectDeadline,
            room.CreatedAt,
            match?.Id);
    }

    private static RatingChangeDto? ChangeFor(GameMatch match, string userId)
    {
        var isP1 = match.Player1UserId == userId;
        if (!isP1 && match.Player2UserId != userId) return null;
        var oldRating = isP1 ? match.Player1RatingBefore : match.Player2RatingBefore;
        var newRating = isP1 ? match.Player1RatingAfter : match.Player2RatingAfter;
        if (newRating is null) return null;
        var result = match.Outcome switch
        {
            MatchOutcome.Draw => "draw",
            MatchOutcome.Player1Win => isP1 ? "win" : "loss",
            MatchOutcome.Player2Win => isP1 ? "loss" : "win",
            _ => "draw",
        };
        var oldRank = RankCatalog.GetByRating(oldRating);
        var newRank = RankCatalog.GetByRating(newRating.Value);
        return new RatingChangeDto(
            match.GameType.ToId(),
            result,
            oldRating,
            newRating.Value,
            newRating.Value - oldRating,
            oldRank.Id,
            newRank.Id,
            newRank.Level > oldRank.Level,
            newRank.Level < oldRank.Level,
            newRank.Level == RankCatalog.Max.Level && newRank.Id != oldRank.Id,
            isP1 ? "Đối thủ" : "Đối thủ",
            isP1 ? match.Player2RatingBefore : match.Player1RatingBefore);
    }

    private async Task<GameRoom> LoadRoom(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await db.GameRooms
            .Include(room => room.Seats)
            .Include(room => room.Matches).ThenInclude(match => match.Moves)
            .FirstOrDefaultAsync(room => room.Code == normalized, cancellationToken)
            ?? throw new CheslaxException("ROOM_NOT_FOUND", "Phòng không tồn tại.", 404);
    }

    private async Task<GameMatch> RequirePlaying(GameRoom room, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var match = room.Matches.FirstOrDefault(item => item.Id == room.CurrentMatchId);
        if (match is null || room.Status != MatchStatus.Playing || match.Status != MatchStatus.Playing)
        {
            throw new CheslaxException("ROOM_ENDED", "Ván chưa bắt đầu hoặc đã kết thúc.", 409);
        }

        return match;
    }

    private static GameRoomSeat RequireSeat(GameRoom room, string userId) =>
        room.Seats.FirstOrDefault(seat => seat.UserId == userId)
        ?? throw new CheslaxException("UNAUTHORIZED", "Bạn không thuộc phòng này.", 403);

    private async Task EnsureNotBusy(string userId, CancellationToken cancellationToken)
    {
        if (await db.ActiveMatchLocks.AnyAsync(item => item.UserId == userId, cancellationToken)
            || await db.MatchmakingTickets.AnyAsync(item => item.UserId == userId && item.Status == MatchmakingTicketStatus.Queued, cancellationToken)
            || await db.GameRoomSeats.AnyAsync(
                seat => seat.UserId == userId && seat.Room != null &&
                        (seat.Room.Status == MatchStatus.Waiting || seat.Room.Status == MatchStatus.Playing),
                cancellationToken))
        {
            throw new CheslaxException("ALREADY_IN_MATCH", "Bạn đang ở trong một ván hoặc hàng chờ.", 409);
        }
    }

    private async Task<User> RequireUser(string userId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return await db.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken)
            ?? throw new CheslaxException("NOT_FOUND", "Không tìm thấy tài khoản.", 404);
    }

    private async Task<int> RatingOf(string userId, GameType game, CancellationToken cancellationToken)
    {
        var rating = await db.PlayerRatings.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId && item.GameType == game, cancellationToken);
        return rating?.Rating ?? RankCatalog.StartingRating;
    }

    private async Task<string> UniqueCode(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var code = new string(RandomNumberGenerator.GetItems(RoomAlphabet, 6));
            if (!await db.GameRooms.AnyAsync(room => room.Code == code, cancellationToken))
            {
                return code;
            }
        }

        throw new CheslaxException("BUSY", "Không tạo được mã phòng duy nhất.", 503);
    }

    private static PersistedPlayState CreateInitialState(GameType game, TimeControl time, DateTimeOffset now)
    {
        var state = new PersistedPlayState();
        if (game == GameType.Chess)
        {
            state.ChessFen = ChessPosition.StartingFen;
            if (time.IsTimed)
            {
                state.WhiteMs = time.BaseMilliseconds;
                state.BlackMs = time.BaseMilliseconds;
                state.ClockUpdatedAt = now;
            }
        }
        else
        {
            state.OAnQuan = OAnQuanRules.CreateInitialState();
        }

        return state;
    }

    private static GameRoomSeat MakeSeat(Guid roomId, string userId, int number, string side, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        RoomId = roomId,
        UserId = userId,
        SeatNumber = number,
        Side = side,
        Connected = true,
        JoinedAt = now,
    };

    private static string PickHostSide(GameType game, string? pref)
    {
        if (game == GameType.OAnQuan) return "0";
        if (pref is "w" or "b") return pref;
        return RandomNumberGenerator.GetInt32(0, 2) == 0 ? "w" : "b";
    }

    private static string OtherSide(GameType game, string side) =>
        game == GameType.OAnQuan ? (side == "0" ? "1" : "0") : (side == "w" ? "b" : "w");

    private static MatchOutcome OutcomeForWinner(GameMatch match, string winnerSide) =>
        winnerSide == match.Player1Side ? MatchOutcome.Player1Win : MatchOutcome.Player2Win;

    private static string? WinnerSide(GameMatch match) => match.Outcome switch
    {
        MatchOutcome.Draw => "draw",
        MatchOutcome.Player1Win => match.Player1Side,
        MatchOutcome.Player2Win => match.Player2Side,
        _ => null,
    };

    private static object ParseSide(GameType game, string side)
    {
        if (game == GameType.OAnQuan && int.TryParse(side, out var n)) return n;
        return side;
    }

    private static string MapStatus(MatchStatus status) => status switch
    {
        MatchStatus.Waiting => "waiting",
        MatchStatus.Playing => "playing",
        MatchStatus.Finished => "ended",
        _ => "abandoned",
    };

    private static char? ParsePromotion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value[0];
    }

    private static IReadOnlyList<string> SeatIds(GameRoom room) => room.Seats.Select(seat => seat.UserId).ToList();

    private static bool AllConnected(GameRoom room) => room.Seats.Count == 2 && room.Seats.All(seat => seat.Connected);
}
