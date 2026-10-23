using Cheslax.Application.Play;
using Cheslax.Domain;
using Cheslax.GameEngine.Chess;
using Cheslax.GameEngine.OAnQuan;
using Microsoft.Extensions.Options;

namespace Cheslax.Infrastructure.Play;

public sealed class AiOpponentService(IOptions<MatchmakingOptions> options) : IAIOpponentService
{
    private readonly MatchmakingOptions _options = options.Value;

    public RankedAiPlan Plan(GameType game, int playerRating)
    {
        var band = AiDifficultyPolicy.Resolve(playerRating, _options.DifficultiesFor(game));
        var policy = string.IsNullOrWhiteSpace(_options.AiRatingPolicy)
            ? AiRatingPolicies.FixedOpponentRating
            : _options.AiRatingPolicy;
        return new RankedAiPlan(
            band.Id,
            band.Label,
            band.OpponentRating,
            policy,
            band.SearchDepth,
            band.Noise,
            band.BlunderChance,
            band.MaxNodes,
            band.MaxTimeMs,
            band.Quiesce);
    }

    public ChessLegalMove ChooseChessMove(ChessPosition position, RankedAiPlan plan, CancellationToken cancellationToken) =>
        ChessAi.ChooseMove(
            position,
            plan.SearchDepth,
            plan.Noise,
            plan.BlunderChance,
            plan.MaxNodes,
            plan.MaxTimeMs,
            plan.Quiesce,
            cancellationToken);

    public (int Pit, string Direction) ChooseOAnQuanMove(OAnQuanState state, RankedAiPlan plan, CancellationToken cancellationToken)
    {
        var move = OAnQuanAi.ChooseMove(state, plan.SearchDepth, plan.BlunderChance, cancellationToken);
        return (move.Pit, move.Direction == OAnQuanDirection.Clockwise ? "cw" : "ccw");
    }
}
