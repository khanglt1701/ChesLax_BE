using Cheslax.Domain;
using Cheslax.GameEngine.Chess;
using Cheslax.GameEngine.OAnQuan;

namespace Cheslax.Application.Play;

public sealed record RankedAiPlan(
    string DifficultyId,
    string DifficultyLabel,
    int OpponentRating,
    string RatingPolicy,
    int SearchDepth,
    int Noise,
    double BlunderChance,
    int MaxNodes,
    int MaxTimeMs,
    bool Quiesce);

public interface IAIOpponentService
{
    RankedAiPlan Plan(GameType game, int playerRating);
    ChessLegalMove ChooseChessMove(ChessPosition position, RankedAiPlan plan, CancellationToken cancellationToken);
    (int Pit, string Direction) ChooseOAnQuanMove(OAnQuanState state, RankedAiPlan plan, CancellationToken cancellationToken);
}

public interface IHumanMatchmakingService
{
    Task MatchQueuedHumansAsync(CancellationToken cancellationToken);
}

public interface IRatingService
{
    Task ApplyIfNeededAsync(GameMatch match, CancellationToken cancellationToken);
}
