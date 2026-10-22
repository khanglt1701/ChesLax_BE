using Cheslax.Application.Abstractions;
using Cheslax.Application.Auth;
using Cheslax.Application.Common;
using Microsoft.AspNetCore.Identity;

namespace Cheslax.Infrastructure.Identity;

public sealed class IdentityAuthService(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
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

        var user = new ApplicationUser
        {
            UserName = username,
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            AvatarId = "seal-1",
            CreatedAt = clock.UtcNow,
            EmailConfirmed = true,
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            throw new CheslaxException("REGISTER_FAILED", "Không tạo được tài khoản.", 400);
        }

        await signIn.SignInAsync(user, isPersistent: true);
        return ToDto(user);
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

        return ToDto(user);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        await signIn.SignOutAsync();
    }

    public async Task<UserDto> GetCurrentAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId)
            ?? throw new CheslaxException("NOT_FOUND", "Không tìm thấy tài khoản.", 404);
        return ToDto(user);
    }

    public async Task<UserDto> UpdateCurrentAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken)
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

        return ToDto(user);
    }

    public async Task<PublicProfileDto?> GetPublicAsync(string username, CancellationToken cancellationToken)
    {
        var user = await users.FindByNameAsync(username.Trim());
        return user is null
            ? null
            : new PublicProfileDto(user.UserName!, user.DisplayName, user.AvatarId, user.CreatedAt);
    }

    private static UserDto ToDto(ApplicationUser user) =>
        new(user.UserName!, user.DisplayName, user.AvatarId, user.CreatedAt);
}
