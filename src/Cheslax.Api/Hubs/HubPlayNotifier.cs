using Cheslax.Application.Play;
using Microsoft.AspNetCore.SignalR;

namespace Cheslax.Api.Hubs;

public sealed class HubPlayNotifier(IHubContext<GameHub> hub, IServiceScopeFactory scopes) : IPlayNotifier
{
    public async Task RoomUpdatedAsync(string roomCode, IReadOnlyList<string> userIds, CancellationToken cancellationToken)
    {
        foreach (var userId in userIds)
        {
            await using var scope = scopes.CreateAsyncScope();
            var play = scope.ServiceProvider.GetRequiredService<IPlayService>();
            try
            {
                var dto = await play.GetRoomAsync(userId, roomCode, cancellationToken);
                await hub.Clients.User(userId).SendAsync("RoomState", dto, cancellationToken);
            }
            catch
            {
                // Viewer may have left.
            }
        }
    }

    public Task QueueUpdatedAsync(string userId, QueueStatusDto status, CancellationToken cancellationToken) =>
        hub.Clients.User(userId).SendAsync("QueueStatus", status, cancellationToken);

    public Task MatchFoundAsync(string userId, MatchFoundDto found, CancellationToken cancellationToken) =>
        hub.Clients.User(userId).SendAsync("MatchFound", found, cancellationToken);
}
