using FluentValidation;

namespace Cheslax.Application.Catalog;

public sealed record LeaderboardEntryDto(
    int Rank,
    string Username,
    string DisplayName,
    string AvatarId,
    int Rating,
    int GamesPlayed);

public sealed record LeaderboardDto(
    string GameId,
    IReadOnlyList<LeaderboardEntryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    LeaderboardEntryDto? You);

public interface ILeaderboardService
{
    Task<LeaderboardDto> GetAsync(string gameId, int page, int pageSize, string? currentUserId, CancellationToken cancellationToken);
}

public sealed record UserPreferenceDto(
    string Theme,
    string ChessBoardTheme,
    string ChessPieceSet,
    string OAnQuanTheme,
    DateTimeOffset UpdatedAt);

public sealed record UpdatePreferenceRequest(
    string Theme,
    string ChessBoardTheme,
    string ChessPieceSet,
    string OAnQuanTheme);

public interface IPreferenceService
{
    Task<UserPreferenceDto> GetAsync(string userId, CancellationToken cancellationToken);
    Task<UserPreferenceDto> UpdateAsync(string userId, UpdatePreferenceRequest request, CancellationToken cancellationToken);
}

public sealed class UpdatePreferenceRequestValidator : AbstractValidator<UpdatePreferenceRequest>
{
    public UpdatePreferenceRequestValidator()
    {
        RuleFor(x => x.Theme).Must(value => value is "light" or "dark");
        RuleFor(x => x.ChessBoardTheme).Must(value =>
            value is "wood" or "walnut" or "ivory" or "tournament" or "marble" or "modern");
        RuleFor(x => x.ChessPieceSet).Must(value => value is "fantasy" or "classic" or "modern");
        RuleFor(x => x.OAnQuanTheme).Must(value => value is "wood" or "stone" or "modern");
    }
}
