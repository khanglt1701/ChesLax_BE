using Cheslax.Application.Play;

namespace Cheslax.Infrastructure.Play;

public sealed class NullPlayNotifier : IPlayNotifier
{
    public Task RoomUpdatedAsync(string roomCode, IReadOnlyList<string> userIds, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task QueueUpdatedAsync(string userId, QueueStatusDto status, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task MatchFoundAsync(string userId, MatchFoundDto found, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
