using Cheslax.Application.Play;

namespace Cheslax.Api.Hosting;

public sealed class GameTickHostedService(IServiceScopeFactory scopes, ILogger<GameTickHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var play = scope.ServiceProvider.GetRequiredService<IPlayService>();
                var matchmaking = scope.ServiceProvider.GetRequiredService<IMatchmakingService>();
                await play.TickAsync(stoppingToken);
                await matchmaking.MatchOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Game tick failed.");
            }
        }
    }
}
