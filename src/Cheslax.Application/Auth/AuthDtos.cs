using FluentValidation;

namespace Cheslax.Application.Auth;

public sealed record RegisterRequest(string Username, string DisplayName, string Email, string Password);

public sealed record LoginRequest(string Username, string Password);

public sealed record UpdateProfileRequest(string DisplayName, string AvatarId);

public sealed record UserDto(string Username, string DisplayName, string AvatarId, DateTimeOffset CreatedAt);

public sealed record GameRatingDto(
    string GameId,
    int Rating,
    int Wins,
    int Losses,
    int Draws,
    int GamesPlayed,
    int HighestRating,
    DateTimeOffset? UpdatedAt);

public sealed record RatingHistoryDto(
    Guid Id,
    Guid MatchId,
    string GameId,
    DateTimeOffset At,
    int OldRating,
    int NewRating,
    int Delta,
    string OldRankId,
    string NewRankId,
    string Result,
    string OpponentName,
    int OpponentRating);

public sealed record ProfileDto(
    string Username,
    string DisplayName,
    string AvatarId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<GameRatingDto> Ratings,
    IReadOnlyList<RatingHistoryDto> History);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().Matches("^[a-zA-Z0-9_]{3,24}$")
            .WithMessage("Tên đăng nhập 3–24 ký tự, chỉ chữ, số hoặc gạch dưới.");
        RuleFor(x => x.DisplayName).NotEmpty().Length(1, 40);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
