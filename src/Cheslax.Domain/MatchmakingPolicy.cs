namespace Cheslax.Domain;

public sealed class MatchmakingOptions
{
    public const string SectionName = "Matchmaking";

    public int KFactor { get; set; } = RankCatalog.DefaultKFactor;
    public int ReconnectGraceSeconds { get; set; } = 90;
    public int ExpandAfterSeconds { get; set; } = 30;
    public int ExpandEverySeconds { get; set; } = 10;
    public int ExpandBy { get; set; } = 50;
    public int MaxSpread { get; set; } = 800;
    public IReadOnlyList<MatchmakingBand> Bands { get; set; } =
    [
        new(0, 100),
        new(10, 200),
        new(20, 300),
    ];
}

public sealed record MatchmakingBand(int AfterSeconds, int MaxSpread);

public static class MatchmakingPolicy
{
    public static int AllowedSpread(TimeSpan waited, MatchmakingOptions options)
    {
        var seconds = Math.Max(0, waited.TotalSeconds);
        var spread = options.Bands.Count == 0 ? 100 : options.Bands[0].MaxSpread;
        foreach (var band in options.Bands.OrderBy(item => item.AfterSeconds))
        {
            if (seconds >= band.AfterSeconds)
            {
                spread = band.MaxSpread;
            }
        }

        if (seconds <= options.ExpandAfterSeconds)
        {
            return Math.Min(spread, options.MaxSpread);
        }

        var extraSteps = (int)Math.Floor((seconds - options.ExpandAfterSeconds) / Math.Max(1, options.ExpandEverySeconds));
        return Math.Min(options.MaxSpread, spread + extraSteps * options.ExpandBy);
    }

    public static MatchmakingTicket? FindClosestOpponent(
        MatchmakingTicket seeker,
        IReadOnlyList<MatchmakingTicket> candidates,
        int allowedSpread,
        IReadOnlySet<string>? busyUserIds = null)
    {
        MatchmakingTicket? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            if (candidate.Id == seeker.Id || candidate.UserId == seeker.UserId)
            {
                continue;
            }

            if (candidate.GameType != seeker.GameType || candidate.Status != MatchmakingTicketStatus.Queued)
            {
                continue;
            }

            if (busyUserIds is not null && busyUserIds.Contains(candidate.UserId))
            {
                continue;
            }

            var distance = Math.Abs(candidate.RatingAtEnqueue - seeker.RatingAtEnqueue);
            if (distance > allowedSpread)
            {
                continue;
            }

            if (distance < bestDistance
                || (distance == bestDistance && candidate.EnqueuedAt < (best?.EnqueuedAt ?? DateTimeOffset.MaxValue)))
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }
}
