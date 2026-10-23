using Cheslax.Application.Abstractions;
using Cheslax.Application.Auth;
using Cheslax.Application.Common;
using Cheslax.Domain;
using Cheslax.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cheslax.Infrastructure.Identity;

public sealed class IdentityAuthService(
    UserManager<User> users,
    SignInManager<User> signIn,
    CheslaxDbContext db,
    IClock clock) : IAuthService
{
    private static readonly string[] AllowedAvatars =
        ["seal-1", "seal-2", "seal-3", "seal-4", "seal-5", "seal-6", "seal-7", "seal-8"];

    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var existing = await users.FindByNameAsync(username);
        if (existing is not null)
        {
            throw new CheslaxException("USERNAME_TAKEN", "Không tạo được tài khoản.", 409);
        }

        var emailOwner = await users.FindByEmailAsync(request.Email.Trim());
        if (emailOwner is not null)
        {
            throw new CheslaxException("EMAIL_TAKEN", "Không tạo được tài khoản.", 409);
        }

        var now = clock.UtcNow;
        var user = new User
        {
            UserName = username,
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            AvatarId = "seal-1",
            CreatedAt = now,
            EmailConfirmed = true,
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            throw new CheslaxException("REGISTER_FAILED", "Không tạo được tài khoản.", 400);
        }

        db.UserPreferences.Add(UserPreference.CreateDefault(user.Id, now));
        db.PlayerRatings.Add(PlayerRating.CreateInitial(user.Id, GameType.Chess, now));
        db.PlayerRatings.Add(PlayerRating.CreateInitial(user.Id, GameType.OAnQuan, now));
        await db.SaveChangesAsync(cancellationToken);

        await signIn.SignInAsync(user, isPersistent: true);
        return ToUserDto(user);
    }

    public async Task<UserDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByNameAsync(request.Username.Trim());
        if (user is null)
        {
            throw new CheslaxException("INVALID_CREDENTIALS", "Tên đăng nhập hoặc mật khẩu không đúng.", 401);
        }

        var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            throw new CheslaxException("INVALID_CREDENTIALS", "Tên đăng nhập hoặc mật khẩu không đúng.", 401);
        }

        user.LastSeenAt = clock.UtcNow;
        await users.UpdateAsync(user);
        return ToUserDto(user);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        await signIn.SignOutAsync();
    }

    public async Task<ProfileDto> GetCurrentAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId)
            ?? throw new CheslaxException("NOT_FOUND", "Không tìm thấy tài khoản.", 404);
        return await ToProfileAsync(user, cancellationToken);
    }

    public async Task<ProfileDto> UpdateCurrentAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId)
            ?? throw new CheslaxException("NOT_FOUND", "Không tìm thấy tài khoản.", 404);
        user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? user.DisplayName : request.DisplayName.Trim();
        if (AllowedAvatars.Contains(request.AvatarId))
        {
            user.AvatarId = request.AvatarId;
        }

        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            throw new CheslaxException("UPDATE_FAILED", "Không lưu được hồ sơ.", 400);
        }

        return await ToProfileAsync(user, cancellationToken);
    }

    public async Task<ProfileDto?> GetPublicAsync(string username, CancellationToken cancellationToken)
    {
        var user = await users.FindByNameAsync(username.Trim());
        return user is null || user.IsGuest ? null : await ToProfileAsync(user, cancellationToken);
    }

    public async Task<UserDto> EnsureGuestAsync(GuestRequest request, string? currentUserId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(currentUserId))
        {
            var current = await users.FindByIdAsync(currentUserId)
                ?? throw new CheslaxException("NOT_FOUND", "Không tìm thấy tài khoản.", 404);
            if (!current.IsGuest)
            {
                return ToUserDto(current);
            }

            if (!string.IsNullOrWhiteSpace(request.DisplayName))
            {
                current.DisplayName = request.DisplayName.Trim()[..Math.Min(40, request.DisplayName.Trim().Length)];
            }

            if (!string.IsNullOrWhiteSpace(request.AvatarId) && AllowedAvatars.Contains(request.AvatarId))
            {
                current.AvatarId = request.AvatarId;
            }

            await users.UpdateAsync(current);
            return ToUserDto(current);
        }

        var now = clock.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? "Khách Cheslax"
            : request.DisplayName.Trim();
        if (displayName.Length > 40) displayName = displayName[..40];
        var avatar = request.AvatarId is not null && AllowedAvatars.Contains(request.AvatarId)
            ? request.AvatarId
            : "seal-1";
        var user = new User
        {
            UserName = $"guest_{suffix}",
            Email = $"guest_{suffix}@guest.cheslax.local",
            DisplayName = displayName,
            AvatarId = avatar,
            CreatedAt = now,
            EmailConfirmed = true,
            IsGuest = true,
        };
        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
        {
            throw new CheslaxException("REGISTER_FAILED", "Không tạo được phiên khách.", 400);
        }

        await signIn.SignInAsync(user, isPersistent: true);
        return ToUserDto(user);
    }

    private static UserDto ToUserDto(User user) =>
        new(user.UserName!, user.DisplayName, user.AvatarId, user.CreatedAt, user.IsGuest);

    private async Task<ProfileDto> ToProfileAsync(User user, CancellationToken cancellationToken)
    {
        var ratings = await db.PlayerRatings.AsNoTracking()
            .Where(rating => rating.UserId == user.Id)
            .OrderBy(rating => rating.GameType)
            .ToListAsync(cancellationToken);
        var history = (await db.RatingHistories.AsNoTracking()
            .Where(entry => entry.UserId == user.Id)
            .ToListAsync(cancellationToken))
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(50)
            .ToList();

        return new ProfileDto(
            user.UserName!,
            user.DisplayName,
            user.AvatarId,
            user.CreatedAt,
            ratings.Select(ToRatingDto).ToList(),
            history.Select(ToHistoryDto).ToList(),
            user.IsGuest);
    }

    private static GameRatingDto ToRatingDto(PlayerRating rating) => new(
        rating.GameType.ToId(),
        rating.Rating,
        rating.Wins,
        rating.Losses,
        rating.Draws,
        rating.GamesPlayed,
        rating.PeakRating,
        rating.UpdatedAt);

    private static RatingHistoryDto ToHistoryDto(RatingHistory entry) => new(
        entry.Id,
        entry.MatchId,
        entry.GameType.ToId(),
        entry.CreatedAt,
        entry.OldRating,
        entry.NewRating,
        entry.Delta,
        entry.OldRankId,
        entry.NewRankId,
        ResultId(entry.Result),
        entry.OpponentName,
        entry.OpponentRating);

    private static string ResultId(MatchResultKind result) => result switch
    {
        MatchResultKind.Win => "win",
        MatchResultKind.Loss => "loss",
        MatchResultKind.Draw => "draw",
        _ => "draw",
    };
}
