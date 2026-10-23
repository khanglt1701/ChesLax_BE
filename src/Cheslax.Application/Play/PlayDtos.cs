using System.Text.Json.Serialization;
using Cheslax.Domain;
using FluentValidation;

namespace Cheslax.Application.Play;

public sealed record CreateCasualRoomRequest(string GameId, string? TimeControl, string? ColorPref, string? DisplayName, string? AvatarId);

public sealed record JoinRoomRequest(string? DisplayName, string? AvatarId);

public sealed record EnqueueRankedRequest(string GameId);

public sealed record ChessMoveRequest(string From, string To, string? Promotion, int Version);

public sealed record OAnQuanMoveRequest(int Pit, string Direction, int Version);

public sealed record QueueStatusDto(
    string Status,
    string GameId,
    int Rating,
    int WaitedSeconds,
    int AllowedSpread,
    DateTimeOffset EnqueuedAt,
    MatchFoundDto? Match = null,
    string MatchmakingMode = RankedMatchmakingModes.AIOnly,
    string? OpponentType = null);

public sealed record MatchFoundDto(
    Guid MatchId,
    string RoomCode,
    string GameId,
    string Mode,
    string OpponentType = "human",
    string? AiDifficulty = null,
    string? AiDifficultyLabel = null);

public sealed record RatingChangeDto(
    string GameId,
    string Result,
    int OldRating,
    int NewRating,
    int Delta,
    string OldRankId,
    string NewRankId,
    bool Promoted,
    bool Demoted,
    bool ReachedMaxRank,
    string OpponentName,
    int OpponentRating,
    string OpponentType = "human",
    string? AiDifficulty = null,
    string? AiDifficultyLabel = null);

public sealed record RoomResultDto(
    string Reason,
    string? WinnerSide,
    bool Rated,
    RatingChangeDto? YourChange,
    string OpponentType = "human",
    string? AiDifficulty = null,
    string? AiDifficultyLabel = null);

public sealed record PublicSeatDto(
    [property: JsonPropertyName("playerId")] string UserId,
    string Name,
    string AvatarId,
    bool Connected,
    object Side,
    bool IsAi = false,
    int? Rating = null);

public sealed record ChessPublicDto(
    string Fen,
    string Turn,
    IReadOnlyList<string> History,
    object? LastMove,
    bool InCheck,
    bool IsGameOver,
    string Status,
    object? Clocks);

public sealed record OAnQuanPublicDto(
    object State,
    IReadOnlyList<string> History,
    object? LastMove,
    IReadOnlyList<object> LastEvents);

public sealed record RoomPublicDto(
    string Code,
    string Game,
    string Mode,
    bool Rated,
    string Status,
    int Version,
    string TimeControl,
    object? You,
    IReadOnlyList<PublicSeatDto> Seats,
    bool Waiting,
    ChessPublicDto? Chess,
    OAnQuanPublicDto? OAnQuan,
    RoomResultDto? Result,
    DateTimeOffset? DisconnectDeadline,
    DateTimeOffset CreatedAt,
    Guid? MatchId,
    string OpponentType = "human",
    string? AiDifficulty = null,
    string? AiDifficultyLabel = null,
    string? AiRatingPolicy = null,
    int? OpponentRating = null);

public interface IPlayNotifier
{
    Task RoomUpdatedAsync(string roomCode, IReadOnlyList<string> userIds, CancellationToken cancellationToken);
    Task QueueUpdatedAsync(string userId, QueueStatusDto status, CancellationToken cancellationToken);
    Task MatchFoundAsync(string userId, MatchFoundDto found, CancellationToken cancellationToken);
}

public interface IPlayService
{
    Task<RoomPublicDto> CreateCasualRoomAsync(string userId, CreateCasualRoomRequest request, CancellationToken cancellationToken);
    Task<RoomPublicDto> JoinRoomAsync(string userId, string code, CancellationToken cancellationToken);
    Task<RoomPublicDto> GetRoomAsync(string userId, string code, CancellationToken cancellationToken);
    Task LeaveRoomAsync(string userId, string code, CancellationToken cancellationToken);
    Task SetConnectedAsync(string userId, string? connectionId, bool connected, CancellationToken cancellationToken);
    Task<RoomPublicDto> ChessMoveAsync(string userId, string code, ChessMoveRequest request, CancellationToken cancellationToken);
    Task<RoomPublicDto> OAnQuanMoveAsync(string userId, string code, OAnQuanMoveRequest request, CancellationToken cancellationToken);
    Task<RoomPublicDto> ResignAsync(string userId, string code, CancellationToken cancellationToken);
    Task TickAsync(CancellationToken cancellationToken);
    Task<RoomPublicDto> CreateRankedMatchAsync(string player1Id, string player2Id, string gameId, int rating1, int rating2, CancellationToken cancellationToken);
    Task<RoomPublicDto> CreateRankedAiMatchAsync(string userId, string gameId, int playerRating, CancellationToken cancellationToken);
}

public interface IMatchmakingService
{
    Task<QueueStatusDto> EnqueueAsync(string userId, string gameId, string? connectionId, CancellationToken cancellationToken);
    Task CancelAsync(string userId, CancellationToken cancellationToken);
    Task<QueueStatusDto?> GetStatusAsync(string userId, CancellationToken cancellationToken);
    Task MatchOnceAsync(CancellationToken cancellationToken);
}

public sealed class CreateCasualRoomRequestValidator : AbstractValidator<CreateCasualRoomRequest>
{
    public CreateCasualRoomRequestValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.ColorPref).Must(value => value is null or "w" or "b" or "random");
    }
}

public sealed class EnqueueRankedRequestValidator : AbstractValidator<EnqueueRankedRequest>
{
    public EnqueueRankedRequestValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
    }
}
