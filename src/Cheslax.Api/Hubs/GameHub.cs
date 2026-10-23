using System.Security.Claims;
using Cheslax.Application.Play;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cheslax.Api.Hubs;

[Authorize]
public sealed class GameHub(IPlayService play, IMatchmakingService matchmaking) : Hub
{
    public async Task JoinRoom(string code)
    {
        var userId = UserId();
        await play.SetConnectedAsync(userId, Context.ConnectionId, true, Context.ConnectionAborted);
        var room = await play.JoinRoomAsync(userId, code, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(room.Code));
        await Clients.Caller.SendAsync("RoomState", room);
    }

    public async Task LeaveRoom(string code)
    {
        await play.LeaveRoomAsync(UserId(), code, Context.ConnectionAborted);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(code));
    }

    public Task<RoomPublicDto> ChessMove(string code, ChessMoveRequest request) =>
        play.ChessMoveAsync(UserId(), code, request, Context.ConnectionAborted);

    public Task<RoomPublicDto> OAnQuanMove(string code, OAnQuanMoveRequest request) =>
        play.OAnQuanMoveAsync(UserId(), code, request, Context.ConnectionAborted);

    public Task<RoomPublicDto> Resign(string code) =>
        play.ResignAsync(UserId(), code, Context.ConnectionAborted);

    public Task<QueueStatusDto> EnqueueRanked(string gameId) =>
        matchmaking.EnqueueAsync(UserId(), gameId, Context.ConnectionId, Context.ConnectionAborted);

    public Task CancelQueue() => matchmaking.CancelAsync(UserId(), Context.ConnectionAborted);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await play.SetConnectedAsync(UserId(), Context.ConnectionId, false, CancellationToken.None);
        await base.OnDisconnectedAsync(exception);
    }

    private string UserId() =>
        Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new HubException("Cần đăng nhập hoặc phiên khách.");

    private static string GroupName(string code) => $"room:{code.Trim().ToUpperInvariant()}";
}
